using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace FireChief;

/// <summary>
/// Orchestrates Fire Chief assignment and notification workflows.
/// </summary>
public sealed partial class AssignmentService(
	NotionService notion,
	SlackService slack,
	ILogger<AssignmentService> logger)
{
	/// <summary>
	/// Runs the full assignment workflow: check existing, select candidates, create the roster, notify teams.
	/// </summary>
	public async Task<Result<AssignmentOutcome>> RunAssignmentAsync()
	{
		LogStartingAssignmentWorkflow();

		DateTime nextMonday = CalculateNextMonday();

		// Check for the existing roster
		Result<RosterEntry?> existingResult = await notion.GetRosterForWeekAsync(nextMonday);

		return existingResult switch
		{
			{ IsFailure: true, Error: var error } => Result.Failure<AssignmentOutcome>(error),
			{ IsSuccess: true, Value: not null } =>
				Result.Success<AssignmentOutcome>(new AssignmentAlreadyExists(nextMonday)).Tap(() => LogRosterAlreadyExists(nextMonday)),

			_ => await ContinueAssignmentWorkflowAsync(nextMonday)
		};
	}

	private async Task<Result<AssignmentOutcome>> ContinueAssignmentWorkflowAsync(DateTime nextMonday)
	{
		// Get active members
		Result<List<TeamMember>> membersResult = await notion.GetActiveMembersAsync();

		return membersResult switch
		{
			{ IsFailure: true, Error: var error } => Result.Failure<AssignmentOutcome>(error),
			{ IsSuccess: true, Value: var members } => await ProcessMemberSelectionAsync(members, nextMonday),

			_ => throw new InvalidOperationException("Result must be either Success or Failure")
		};
	}

	private async Task<Result<AssignmentOutcome>> ProcessMemberSelectionAsync(List<TeamMember> members, DateTime nextMonday)
	{
		// Select chief and backup
		Result<(TeamMember chief, TeamMember backup)> selectionResult = SelectChiefAndBackup(members);

		return selectionResult switch
		{
			{ IsFailure: true, Error: var error } => Result.Failure<AssignmentOutcome>(error),
			{ IsSuccess: true, Value: var (chief, backup) } => await CreateAndNotifyAssignmentAsync(chief, backup, nextMonday),

			_ => throw new InvalidOperationException("Result must be either Success or Failure")
		};
	}

	private async Task<Result<AssignmentOutcome>> CreateAndNotifyAssignmentAsync(
		TeamMember chief,
		TeamMember backup,
		DateTime nextMonday)
	{
		LogSelectedChiefAndBackup(chief.Name, backup.Name);

		AssignmentResult assignment = new(chief, backup, nextMonday);

		// Fetch current week's roster for handover notification
		DateTime thisMonday = CalculateThisMonday();
		Result<RosterEntry?> currentRosterResult = await notion.GetRosterForWeekAsync(thisMonday);

		// Create roster entry
		RosterEntry entry = new(
			Id: "",
			WeekStart: nextMonday,
			ChiefId: chief.Id,
			BackupId: backup.Id
		);

		Result<string> createResult = await notion.CreateRosterEntryAsync(entry);

		return createResult switch
		{
			{ IsFailure: true, Error: var error } => Result.Failure<AssignmentOutcome>(error),
			{ IsSuccess: true, Value: var rosterId } => await FinalizeAssignmentAsync(assignment, rosterId, nextMonday, currentRosterResult),

			_ => throw new InvalidOperationException("Result must be either Success or Failure")
		};
	}

	private async Task<Result<AssignmentOutcome>> FinalizeAssignmentAsync(
		AssignmentResult assignment,
		string rosterId,
		DateTime nextMonday,
		Result<RosterEntry?> currentRosterResult)
	{
		// Update chief's last assignment date
		Result updateResult = await notion.UpdateLastChiefDateAsync(assignment.Chief.Id, nextMonday);
		if (updateResult.IsFailure)
			LogFailedToUpdateChiefDate(updateResult.Error);

		// Send notifications and set topic, then store timestamps
		Result<(string publicTs, string internalTs)> notificationResult = await SendNotificationsAsync(assignment);
		if (notificationResult.IsSuccess)
		{
			await notion.UpdateRosterMessageTimestampsAsync(rosterId, notificationResult.Value.publicTs, notificationResult.Value.internalTs);
		}
		else
		{
			LogFailedToSendNotifications(notificationResult.Error);
		}

		// Send handover recommendation if current roster exists
		if (currentRosterResult is { IsSuccess: true, Value: not null })
		{
			Result handoverResult = await SendHandoverRecommendationAsync(currentRosterResult.Value, assignment);
			// Log but don't fail workflow if handover fails
			if (handoverResult.IsFailure)
				LogFailedToSendHandoverRecommendation(handoverResult.Error);
		}

		LogAssignmentWorkflowCompleted();
		return Result.Success<AssignmentOutcome>(new AssignmentCreated(assignment));
	}

	/// <summary>
	/// Sends a Monday welcome reminder to the current Fire Chief.
	/// </summary>
	public async Task<Result> RunMondayReminderAsync()
	{
		LogStartingMondayReminderWorkflow();
		DateTime thisMonday = CalculateThisMonday();
		Result<RosterEntry?> rosterResult = await notion.GetRosterForWeekAsync(thisMonday);

		return rosterResult switch
		{
			{ IsFailure: true, Error: var error } => Result.Failure(error),
			{ IsSuccess: true, Value: null } => Result.Failure("No roster found for current week")
				.Tap(() => LogNoRosterFoundForCurrentWeek(thisMonday)),
			{ IsSuccess: true, Value: var roster } => await SendMondayWelcomeAsync(roster),

			_ => throw new InvalidOperationException("Result must be either Success or Failure")
		};
	}

	private async Task<Result> SendMondayWelcomeAsync(RosterEntry roster)
	{
		Result<List<TeamMember>> membersResult = await notion.GetActiveMembersAsync();

		return membersResult switch
		{
			{ IsFailure: true, Error: var error } => Result.Failure(error),
			{ IsSuccess: true, Value: var members } => await ProcessMondayWelcomeAsync(members, roster),

			_ => throw new InvalidOperationException("Result must be either Success or Failure")
		};
	}

	private async Task<Result> ProcessMondayWelcomeAsync(List<TeamMember> members, RosterEntry roster)
	{
		TeamMember? chief = members.FirstOrDefault(m => m.Id == roster.ChiefId);
		TeamMember? backup = members.FirstOrDefault(m => m.Id == roster.BackupId);

		if (chief is null)
		{
			LogChiefNotFoundForRoster(roster.Id);
			return Result.Failure("Chief not found in active members");
		}

		string message = FormatMondayWelcome(chief, backup);
		string topic = FormatChannelTopic(chief, roster.WeekStart);
		Result<string> sendResult = await slack.SendAndSetInternalTopicAsync(message, topic);

		return sendResult.Match(
			onSuccess: _ => Result.Success(),
			onFailure: Result.Failure
		);
	}

	private async Task<Result<(string publicTs, string internalTs)>> SendNotificationsAsync(AssignmentResult assignment)
	{
		// Create topic for both channels
		string topic = FormatChannelTopic(assignment.Chief, assignment.WeekStart);

		// Send and set topic for both channels in parallel
		Task<Result<string>> publicTask = slack.SendAndSetPublicTopicAsync(assignment.FormatPublicAnnouncement(), topic);
		Task<Result<string>> internalTask = slack.SendAndSetInternalTopicAsync(assignment.FormatInternalNotification(), topic);

		await Task.WhenAll(publicTask, internalTask);

		Result<string> publicResult = await publicTask;
		Result<string> internalResult = await internalTask;

		// If public fails, fail immediately as we haven't notified anyone publicly
		return publicResult switch
		{
			{ IsFailure: true, Error: var error } => Result.Failure<(string, string)>(error),
			{ IsSuccess: true, Value: var publicTs } => CombineNotificationResults(publicTs, internalResult),

			_ => throw new InvalidOperationException("Result must be either Success or Failure")
		};
	}

	private static Result<(string publicTs, string internalTs)> CombineNotificationResults(
		string publicTs,
		Result<string> internalResult)
	{
		return internalResult switch
		{
			{ IsFailure: true, Error: var error } => Result.Failure<(string, string)>(error),
			{ IsSuccess: true, Value: var internalTs } => Result.Success((publicTs, internalTs)),

			_ => throw new InvalidOperationException("Result must be either Success or Failure")
		};
	}

	private async Task<Result> SendHandoverRecommendationAsync(RosterEntry currentRoster, AssignmentResult incomingAssignment)
	{
		Result<List<TeamMember>> membersResult = await notion.GetActiveMembersAsync();

		return await membersResult.Match(
			onSuccess: async members =>
			{
				TeamMember? outgoingChief = members.FirstOrDefault(m => m.Id == currentRoster.ChiefId);

				if (outgoingChief is null)
				{
					LogCurrentChiefNotFoundForHandover(currentRoster.ChiefId);
					return Result.Failure("Current chief not found");
				}

				string message = FormatHandoverRecommendation(outgoingChief, incomingAssignment.Chief);
				Result<string> sendResult = await slack.SendInternalAsync(message);

				if (sendResult.IsSuccess)
				{
					LogHandoverRecommendationSent(outgoingChief.Name, incomingAssignment.Chief.Name);
					return Result.Success();
				}

				LogFailedToSendHandoverRecommendation(sendResult.Error);
				return Result.Failure(sendResult.Error);
			},
			onFailure: error =>
			{
				LogFailedToGetMembersForHandover(error);
				return Task.FromResult(Result.Failure(error));
			}
		);
	}

	private Result<(TeamMember chief, TeamMember backup)> SelectChiefAndBackup(List<TeamMember> members)
	{
		if (members.Count < 1)
		{
			LogNoActiveTeamMembers();
			return Result.Failure<(TeamMember, TeamMember)>("No active team members found");
		}

		var candidates = members
			.OrderByDescending(m => m.IsVolunteer)
			.ThenBy(m => m.LastChiefDate ?? DateTime.MinValue)
			.ThenBy(_ => Guid.NewGuid())
			.ToList();

		TeamMember chief = candidates[0];
		TeamMember backup = candidates.Count > 1 ? candidates[1] : chief;

		if (candidates.Count == 1)
		{
			LogOnlyOneMemberAvailable(chief.Name);
		}

		return Result.Success((chief, backup));
	}

	private static string FormatMondayWelcome(TeamMember chief, TeamMember? backup)
	{
		string backupLine = backup != null && backup.Id != chief.Id
			? $"\n\nYour backup is <@{backup.SlackId}>"
			: "";

		return $"""
			🔥 Fire Chief Week Start

			<@{chief.SlackId}> – Welcome to your Fire Chief week!

			Your responsibilities this week:
			- [ ] Monitor #team-software for support requests
			- [ ] Triage and create Linear issues as needed
			- [ ] Shield the team from interruptions
			- [ ] Prepare handover notes for next Friday{backupLine}

			Good luck! 🚒
			""";
	}

	private static string FormatHandoverRecommendation(TeamMember outgoingChief, TeamMember incomingChief) =>
		$"""
		🔄 Fire Chief Handover – Next Week

		<@{outgoingChief.SlackId}> → <@{incomingChief.SlackId}>

		The next Fire Chief has been assigned for next week!

		*Outgoing Chief (<@{outgoingChief.SlackId}>):* Please prepare your handover:
		• Review open support tickets
		• Check pending Linear issues
		• Brief on ongoing critical issues
		• Share any context or ongoing work

		*Incoming Chief (<@{incomingChief.SlackId}>):* You're up next week! Consider reaching out to coordinate timing.

		Thanks for your service this week! 🚒
		""";

	private static string FormatChannelTopic(TeamMember chief, DateTime weekStart) =>
		$"🔥 Fire Chief: <@{chief.SlackId}> (Week of {weekStart:MMM d})";

	private static DateTime CalculateNextMonday()
	{
		DateTime today = DateTime.UtcNow.Date;
		int daysUntilMonday = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
		return daysUntilMonday == 0 ? today : today.AddDays(daysUntilMonday);
	}

	private static DateTime CalculateThisMonday()
	{
		DateTime today = DateTime.UtcNow.Date;
		int daysSinceMonday = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
		return today.AddDays(-daysSinceMonday);
	}
}
