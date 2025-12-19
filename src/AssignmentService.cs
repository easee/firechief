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

		return createResult switch
		{
			{ IsFailure: true, Error: var error } => Result.Failure<AssignmentOutcome>(error),
			{ IsSuccess: true, Value: var rosterId } => await FinalizeAssignmentAsync(assignment, rosterId, nextMonday),

			_ => throw new InvalidOperationException("Result must be either Success or Failure")
		};
	}

	private async Task<Result<AssignmentOutcome>> FinalizeAssignmentAsync(
		AssignmentResult assignment,
		string rosterId,
		DateTime nextMonday)
	{
		// Update chief's last assignment date
		Result updateResult = await notion.UpdateLastChiefDateAsync(assignment.Chief.Id, nextMonday);
		if (updateResult.IsFailure)
			LogFailedToUpdateChiefDate(updateResult.Error);

		// Send notifications and pin them, then store timestamps
		Result<(string publicTs, string internalTs)> notificationResult = await SendNotificationsAsync(assignment);
		if (notificationResult.IsSuccess)
		{
			await notion.UpdateRosterMessageTimestampsAsync(rosterId, notificationResult.Value.publicTs, notificationResult.Value.internalTs);
		}
		else
		{
			LogFailedToSendNotifications(notificationResult.Error);
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

		return rosterResult switch
		{
			{ IsFailure: true, Error: var error } => Result.Failure(error),
			{ IsSuccess: true, Value: null } => Result.Failure("No roster found for current week")
				.Tap(() => LogNoRosterFoundForCurrentWeek(thisMonday)),
			{ IsSuccess: true, Value: var roster } => await SendReminderToChiefAsync(roster),

			_ => throw new InvalidOperationException("Result must be either Success or Failure")
		};
	}

	private async Task<Result> SendReminderToChiefAsync(RosterEntry roster)
	{
		Result<List<TeamMember>> membersResult = await notion.GetActiveMembersAsync();

		return membersResult switch
		{
			{ IsFailure: true, Error: var error } => Result.Failure(error),
			{ IsSuccess: true, Value: var members } => await ProcessChiefReminderAsync(members, roster),

			_ => throw new InvalidOperationException("Result must be either Success or Failure")
		};
	}

	private async Task<Result> ProcessChiefReminderAsync(List<TeamMember> members, RosterEntry roster)
	{
		TeamMember? chief = members.FirstOrDefault(m => m.Id == roster.ChiefId);

		return chief switch
		{
			null => Result.Failure("Chief not found in active members").Tap(() => LogChiefNotFoundForRoster(roster.Id)),
			_ => await SendHandoverReminderAsync(chief)
		};
	}

	private async Task<Result> SendHandoverReminderAsync(TeamMember chief)
	{
		string message = FormatHandoverReminder(chief);
		Result<string> sendResult = await slack.SendAndPinInternalAsync(message);

		return sendResult.Match(
			onSuccess: _ => Result.Success(),
			onFailure: error => Result.Failure(error)
		);
	}

	private async Task<Result<(string publicTs, string internalTs)>> SendNotificationsAsync(AssignmentResult assignment)
	{
		// Send and pin both messages in parallel
		Task<Result<string>> publicTask = slack.SendAndPinPublicAsync(assignment.FormatPublicAnnouncement());
		Task<Result<string>> internalTask = slack.SendAndPinInternalAsync(assignment.FormatInternalNotification());

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

	private async Task UnpinPreviousAssignmentAsync(DateTime upcomingWeekStart)
	{
		// Unpin ALL pinned messages in both channels to avoid pin saturation
		// This ensures only the latest assignment will be pinned
		// Uses Slack API to get all pins, so it catches messages not tracked in Notion
		Result<int> unpinResult = await slack.UnpinAllMessagesAsync();

		if (unpinResult.IsSuccess)
			LogUnpinnedAllPreviousMessages();
		else
			LogFailedToUnpinAllMessages(unpinResult.Error);
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
