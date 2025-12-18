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
		if (existingResult.IsFailure)
			return Result.Failure<AssignmentOutcome>(existingResult.Error);

		if (existingResult.Value is not null)
		{
			LogRosterAlreadyExists(nextMonday);
			return Result.Success<AssignmentOutcome>(new AssignmentAlreadyExists(nextMonday));
		}

		// Get active members
		Result<List<TeamMember>> membersResult = await notion.GetActiveMembersAsync();
		if (membersResult.IsFailure)
			return Result.Failure<AssignmentOutcome>(membersResult.Error);

		// Select chief and backup
		Result<(TeamMember chief, TeamMember backup)> selectionResult = SelectChiefAndBackup(membersResult.Value);
		if (selectionResult.IsFailure)
			return Result.Failure<AssignmentOutcome>(selectionResult.Error);

		(TeamMember chief, TeamMember backup) = selectionResult.Value;
		LogSelectedChiefAndBackup(chief.Name, backup.Name);

		AssignmentResult assignment = new(chief, backup, nextMonday);

		// Unpin previous week's assignment messages
		await UnpinPreviousAssignmentAsync(nextMonday);

		// Create roster entry
		RosterEntry entry = new(
			Id: "",
			WeekStart: nextMonday,
			ChiefId: chief.Id,
			BackupId: backup.Id
		);

		Result<string> createResult = await notion.CreateRosterEntryAsync(entry);
		if (createResult.IsFailure)
			return Result.Failure<AssignmentOutcome>(createResult.Error);

		string rosterId = createResult.Value;

		// Update chief's last assignment date
		Result updateResult = await notion.UpdateLastChiefDateAsync(chief.Id, nextMonday);
		if (updateResult.IsFailure)
			LogFailedToUpdateChiefDate(updateResult.Error);

		// Send notifications and pin them, then store timestamps
		Result<(string publicTs, string internalTs)> notificationResult = await SendNotificationsAsync(assignment);
		if (notificationResult.IsFailure)
		{
			LogFailedToSendNotifications(notificationResult.Error);
		}
		else
		{
			// Store message timestamps in Notion for future unpinning
			await notion.UpdateRosterMessageTimestampsAsync(rosterId, notificationResult.Value.publicTs, notificationResult.Value.internalTs);
		}

		LogAssignmentWorkflowCompleted();
		return Result.Success<AssignmentOutcome>(new AssignmentCreated(assignment));
	}

	/// <summary>
	/// Sends a Friday reminder to the current Fire Chief.
	/// </summary>
	public async Task<Result> RunFridayReminderAsync()
	{
		LogStartingFridayReminderWorkflow();

		DateTime thisMonday = CalculateThisMonday();

		Result<RosterEntry?> rosterResult = await notion.GetRosterForWeekAsync(thisMonday);
		if (rosterResult.IsFailure)
			return Result.Failure(rosterResult.Error);

		if (rosterResult.Value is not { } roster)
		{
			LogNoRosterFoundForCurrentWeek(thisMonday);
			return Result.Failure("No roster found for current week");
		}

		Result<List<TeamMember>> membersResult = await notion.GetActiveMembersAsync();
		if (membersResult.IsFailure)
			return Result.Failure(membersResult.Error);

		TeamMember? chief = membersResult.Value.FirstOrDefault(m => m.Id == roster.ChiefId);
		if (chief is null)
		{
			LogChiefNotFoundForRoster(roster.Id);
			return Result.Failure("Chief not found in active members");
		}

		string message = FormatHandoverReminder(chief);
		Result<string> sendResult = await slack.SendAndPinInternalAsync(message);

		return sendResult.IsSuccess
			? Result.Success()
			: Result.Failure(sendResult.Error);
	}

	private async Task<Result<(string publicTs, string internalTs)>> SendNotificationsAsync(AssignmentResult assignment)
	{
		// Send and pin both messages in parallel
		Task<Result<string>> publicTask = slack.SendAndPinPublicAsync(assignment.FormatPublicAnnouncement());
		Task<Result<string>> internalTask = slack.SendAndPinInternalAsync(assignment.FormatInternalNotification());

		await Task.WhenAll(publicTask, internalTask);

		Result<string> publicResult = await publicTask;
		if (publicResult.IsFailure) // At this point we haven't notified anyone publicly, so fail
			return Result.Failure<(string, string)>(publicResult.Error);

		Result<string> internalResult = await internalTask;

		var notificationResult = Result.Combine(publicResult, internalResult);

		return notificationResult.IsFailure switch
		{
			true => Result.Failure<(string, string)>(notificationResult.Error),
			_ => Result.Success((publicResult.Value, internalResult.Value))
		};
	}

	private async Task UnpinPreviousAssignmentAsync(DateTime upcomingWeekStart)
	{
		// Unpin ALL pinned messages in both channels to avoid pin saturation
		// This ensures only the latest assignment will be pinned
		// Uses Slack API to get all pins, so it catches messages not tracked in Notion
		Result<int> unpinResult = await slack.UnpinAllMessagesAsync();

		if (unpinResult.IsSuccess)
		{
			LogUnpinnedAllPreviousMessages();
		}
		else
		{
			LogFailedToUnpinAllMessages(unpinResult.Error);
		}
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

	private static string FormatHandoverReminder(TeamMember chief) =>
		$"""
		🔥 Fire Chief Handover Reminder

		<@{chief.SlackId}> – Your Fire Chief rotation ends this week!

		Please prepare your handover:
		- [ ] Review open support tickets
		- [ ] Check pending Linear issues
		- [ ] Brief on ongoing critical issues
		- [ ] Schedule handover with next Chief

		Next Chief will be assigned Monday morning.
		""";

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
