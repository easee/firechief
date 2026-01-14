using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notion.Client;

namespace FireChief;

/// <summary>
/// Service for interacting with Notion databases to manage a Fire Chief roster.
/// </summary>
public sealed partial class NotionService(IOptions<AppConfig> config, ILogger<NotionService> logger)
{
	private readonly INotionClient _client = NotionClientFactory.Create(new ClientOptions { AuthToken = config.Value.NotionToken });

	/// <summary>
	/// Retrieves all active team members from the Notion database.
	/// </summary>
	public async Task<Result<List<TeamMember>>> GetActiveMembersAsync()
	{
		try
		{
			CheckboxFilter filter = new("Active", equal: true);
			DatabaseQueryResponse response = await _client.Databases.QueryAsync(
				config.Value.TeamDatabaseId,
				new DatabasesQueryParameters { Filter = filter }
			);

			var members = response.Results.OfType<Page>().Select(MapToTeamMember).ToList();
			LogRetrievedMembers(members.Count);
			return Result.Success(members);
		}
		catch (Exception ex)
		{
			LogFailedToRetrieveMembers(ex);
			return Result.Failure<List<TeamMember>>($"Notion API error: {ex.Message}");
		}
	}

	/// <summary>
	/// Gets the roster entry for a specific week if it exists.
	/// </summary>
	public async Task<Result<RosterEntry?>> GetRosterForWeekAsync(DateTime weekStart)
	{
		try
		{
			DatabaseQueryResponse response = await _client.Databases.QueryAsync(
				config.Value.RosterDatabaseId,
				new DatabasesQueryParameters()
			);

			Page? match = response.Results.OfType<Page>().FirstOrDefault(i =>
				i.Properties.TryGetValue("Week", out PropertyValue? weekProp) &&
				weekProp is DatePropertyValue { Date.Start: not null } d &&
				d.Date.Start == weekStart.Date
			);

			return Result.Success(match is null ? null : MapToRosterEntry(match, weekStart));
		}
		catch (Exception ex)
		{
			LogFailedToRetrieveRoster(ex, weekStart);
			return Result.Failure<RosterEntry?>($"Notion API error: {ex.Message}");
		}
	}

	/// <summary>
	/// Gets all roster entries that have pinned message timestamps.
	/// </summary>
	public async Task<Result<List<RosterEntry>>> GetAllRostersWithPinsAsync()
	{
		try
		{
			DatabaseQueryResponse response = await _client.Databases.QueryAsync(
				config.Value.RosterDatabaseId,
				new DatabasesQueryParameters()
			);

			List<RosterEntry> rosters = [];
			foreach (Page page in response.Results.OfType<Page>())
			{
				if (page.Properties.TryGetValue("Week", out PropertyValue? weekProp) &&
					weekProp is DatePropertyValue { Date.Start: not null } dateVal)
				{
					RosterEntry roster = MapToRosterEntry(page, dateVal.Date.Start.Value.DateTime);

					// Only include rosters that have at least one message timestamp
					if (!string.IsNullOrWhiteSpace(roster.PublicMessageTs) ||
						!string.IsNullOrWhiteSpace(roster.InternalMessageTs))
					{
						rosters.Add(roster);
					}
				}
			}

			LogFoundRostersWithPins(rosters.Count);
			return Result.Success(rosters);
		}
		catch (Exception ex)
		{
			LogFailedToRetrieveRostersWithPins(ex);
			return Result.Failure<List<RosterEntry>>($"Notion API error: {ex.Message}");
		}
	}

	/// <summary>
	/// Creates a new roster entry in Notion and returns the page ID.
	/// </summary>
	public async Task<Result<string>> CreateRosterEntryAsync(RosterEntry entry)
	{
		try
		{
			PagesCreateParameters createParams = new()
			{
				Parent = new DatabaseParentInput { DatabaseId = config.Value.RosterDatabaseId },
				Properties = new Dictionary<string, PropertyValue>
				{
					["Week"] = new DatePropertyValue { Date = new Date { Start = entry.WeekStart.Date } },
					["Chief"] = new RelationPropertyValue { Relation = [new ObjectId { Id = entry.ChiefId }] },
					["Backup"] = new RelationPropertyValue { Relation = [new ObjectId { Id = entry.BackupId }] },
					["Status"] = new SelectPropertyValue { Select = new SelectOption { Name = "Planned" } }
				}
			};

			Page createdPage = await _client.Pages.CreateAsync(createParams);

			LogCreatedRoster(entry.WeekStart);
			return Result.Success(createdPage.Id);
		}
		catch (Exception ex)
		{
			LogFailedToCreateRoster(ex, entry.WeekStart);
			return Result.Failure<string>($"Notion API error: {ex.Message}");
		}
	}

	/// <summary>
	/// Updates the last chief date for a team member, clears their volunteer flag, and increments their chief count.
	/// </summary>
	public async Task<Result> UpdateLastChiefDateAsync(string memberId, DateTime date)
	{
		try
		{
			// First retrieve the current page to get the current Chief Count
			Page currentPage = await _client.Pages.RetrieveAsync(memberId);
			int currentCount = currentPage.Properties.TryGetValue("Chief Count", out PropertyValue? countProp) &&
							   countProp is NumberPropertyValue { Number: not null } numVal
				? (int)numVal.Number.Value
				: 0;

			Dictionary<string, PropertyValue> props = new()
			{
				["Recent Chief Date"] = new DatePropertyValue { Date = new Date { Start = date.Date } },
				["Volunteer"] = new CheckboxPropertyValue { Checkbox = false },
				["Chief Count"] = new NumberPropertyValue { Number = currentCount + 1 }
			};

			await _client.Pages.UpdatePropertiesAsync(memberId, props);
			LogUpdatedMemberDate(memberId, date);
			return Result.Success();
		}
		catch (Exception ex)
		{
			LogFailedToUpdateMember(ex, memberId);
			return Result.Failure($"Notion API error: {ex.Message}");
		}
	}

	/// <summary>
	/// Updates the Slack message timestamps for a roster entry.
	/// </summary>
	public async Task<Result> UpdateRosterMessageTimestampsAsync(string rosterId, string? publicMessageTs, string? internalMessageTs)
	{
		try
		{
			Dictionary<string, PropertyValue> props = new();

			if (!string.IsNullOrWhiteSpace(publicMessageTs))
			{
				props["Public Message TS"] = new RichTextPropertyValue
				{
					RichText = [new RichTextText { Text = new Text { Content = publicMessageTs } }]
				};
			}

			if (!string.IsNullOrWhiteSpace(internalMessageTs))
			{
				props["Internal Message TS"] = new RichTextPropertyValue
				{
					RichText = [new RichTextText { Text = new Text { Content = internalMessageTs } }]
				};
			}

			if (props.Count > 0)
			{
				await _client.Pages.UpdatePropertiesAsync(rosterId, props);
				LogUpdatedRosterTimestamps(rosterId);
			}

			return Result.Success();
		}
		catch (Exception ex)
		{
			LogFailedToUpdateRosterTimestamps(ex, rosterId);
			return Result.Failure($"Notion API error: {ex.Message}");
		}
	}

	private static TeamMember MapToTeamMember(Page item)
	{
		IDictionary<string, PropertyValue> props = item.Properties;

		string name = props.TryGetValue("Name", out PropertyValue? nameProp) && nameProp is TitlePropertyValue title
			? title.Title.FirstOrDefault()?.PlainText ?? "Unknown"
			: "Unknown";

		string slackId = props.TryGetValue("Slack ID", out PropertyValue? slackProp) && slackProp is RichTextPropertyValue richText
			? richText.RichText.FirstOrDefault()?.PlainText ?? ""
			: "";

		bool isVolunteer = props.TryGetValue("Volunteer", out PropertyValue? volunteerProp) &&
						   volunteerProp is CheckboxPropertyValue { Checkbox: true };

		DateTime? lastDate = props.TryGetValue("Recent Chief Date", out PropertyValue? dateProp) &&
							 dateProp is DatePropertyValue { Date: not null } dateVal
			? dateVal.Date.Start?.DateTime
			: null;

		int chiefCount = props.TryGetValue("Chief Count", out PropertyValue? countProp) &&
						 countProp is NumberPropertyValue { Number: not null } numVal
			? (int)numVal.Number.Value
			: 0;

		return new TeamMember(
			Id: item.Id,
			Name: name,
			SlackId: slackId,
			LastChiefDate: lastDate,
			IsActive: true,
			IsVolunteer: isVolunteer,
			ChiefCount: chiefCount
		);
	}

	private static RosterEntry MapToRosterEntry(Page match, DateTime weekStart)
	{
		IDictionary<string, PropertyValue> props = match.Properties;

		string chiefId = props.TryGetValue("Chief", out PropertyValue? chiefProp) &&
						 chiefProp is RelationPropertyValue chiefRel && chiefRel.Relation.Count > 0
			? chiefRel.Relation[0].Id
			: "";

		string backupId = props.TryGetValue("Backup", out PropertyValue? backupProp) &&
						  backupProp is RelationPropertyValue backupRel && backupRel.Relation.Count > 0
			? backupRel.Relation[0].Id
			: "";

		string status = props.TryGetValue("Status", out PropertyValue? statusProp) &&
						statusProp is SelectPropertyValue { Select: not null } s
			? s.Select.Name
			: "Planned";

		string? publicMessageTs = props.TryGetValue("Public Message TS", out PropertyValue? publicTsProp) &&
								  publicTsProp is RichTextPropertyValue publicRichText &&
								  publicRichText.RichText.Count > 0
			? publicRichText.RichText[0].PlainText
			: null;

		string? internalMessageTs = props.TryGetValue("Internal Message TS", out PropertyValue? internalTsProp) &&
									internalTsProp is RichTextPropertyValue internalRichText &&
									internalRichText.RichText.Count > 0
			? internalRichText.RichText[0].PlainText
			: null;

		return new RosterEntry(
			Id: match.Id,
			WeekStart: weekStart,
			ChiefId: chiefId,
			BackupId: backupId,
			Status: status,
			PublicMessageTs: publicMessageTs,
			InternalMessageTs: internalMessageTs
		);
	}
}
