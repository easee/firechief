using Microsoft.Extensions.Logging;

namespace FireChief;

public sealed partial class SlackService
{
	[LoggerMessage(LogLevel.Warning, "Bot token not configured")]
	partial void LogBotTokenNotConfigured();

	[LoggerMessage(LogLevel.Error, "Failed to send internal message: {Error}")]
	partial void LogFailedToSendInternalMessage(string? error);

	[LoggerMessage(LogLevel.Information, "Sent internal message")]
	partial void LogSentInternalMessage();

	[LoggerMessage(LogLevel.Error, "Failed to send internal message")]
	partial void LogFailedToSendInternalMessage(Exception exception);

	[LoggerMessage(LogLevel.Error, "Failed to send public message: {Error}")]
	partial void LogFailedToSendPublicMessage(string? error);

	[LoggerMessage(LogLevel.Information, "Sent public message")]
	partial void LogSentPublicMessage();

	[LoggerMessage(LogLevel.Error, "Failed to send public message")]
	partial void LogFailedToSendPublicMessage(Exception exception);

	[LoggerMessage(LogLevel.Information, "Set topic for channel {ChannelId}: {Topic}")]
	partial void LogSetTopic(string channelId, string topic);

	[LoggerMessage(LogLevel.Warning, "Failed to set topic for channel {ChannelId}: {Error}")]
	partial void LogFailedToSetTopic(string channelId, string error);

	[LoggerMessage(LogLevel.Warning, "Failed to set topic for channel {ChannelId}")]
	partial void LogFailedToSetTopic(Exception exception, string channelId);
}

public sealed partial class NotionService
{
	[LoggerMessage(LogLevel.Information, "Retrieved {Count} active members")]
	partial void LogRetrievedMembers(int count);

	[LoggerMessage(LogLevel.Error, "Failed to retrieve members")]
	partial void LogFailedToRetrieveMembers(Exception exception);

	[LoggerMessage(LogLevel.Error, "Failed to retrieve roster for week {WeekStart}")]
	partial void LogFailedToRetrieveRoster(Exception exception, DateTime weekStart);

	[LoggerMessage(LogLevel.Information, "Found {Count} rosters with pins")]
	partial void LogFoundRostersWithPins(int count);

	[LoggerMessage(LogLevel.Error, "Failed to retrieve rosters with pins")]
	partial void LogFailedToRetrieveRostersWithPins(Exception exception);

	[LoggerMessage(LogLevel.Information, "Created roster for week {WeekStart}")]
	partial void LogCreatedRoster(DateTime weekStart);

	[LoggerMessage(LogLevel.Error, "Failed to create roster for week {WeekStart}")]
	partial void LogFailedToCreateRoster(Exception exception, DateTime weekStart);

	[LoggerMessage(LogLevel.Information, "Updated member {MemberId} last chief date to {Date}")]
	partial void LogUpdatedMemberDate(string memberId, DateTime date);

	[LoggerMessage(LogLevel.Error, "Failed to update member {MemberId}")]
	partial void LogFailedToUpdateMember(Exception exception, string memberId);

	[LoggerMessage(LogLevel.Information, "Updated roster {RosterId} timestamps")]
	partial void LogUpdatedRosterTimestamps(string rosterId);

	[LoggerMessage(LogLevel.Error, "Failed to update roster {RosterId} timestamps")]
	partial void LogFailedToUpdateRosterTimestamps(Exception exception, string rosterId);
}

public sealed partial class AssignmentService
{
	[LoggerMessage(LogLevel.Information, "Starting assignment workflow")]
	partial void LogStartingAssignmentWorkflow();

	[LoggerMessage(LogLevel.Information, "Roster already exists for {Date}, skipping assignment")]
	partial void LogRosterAlreadyExists(DateTime date);

	[LoggerMessage(LogLevel.Information, "Selected Chief: {Chief}, Backup: {Backup}")]
	partial void LogSelectedChiefAndBackup(string chief, string backup);

	[LoggerMessage(LogLevel.Warning, "Failed to update chief date: {Error}")]
	partial void LogFailedToUpdateChiefDate(string error);

	[LoggerMessage(LogLevel.Warning, "Failed to send notifications: {Error}")]
	partial void LogFailedToSendNotifications(string error);

	[LoggerMessage(LogLevel.Information, "Assignment workflow completed successfully")]
	partial void LogAssignmentWorkflowCompleted();

	[LoggerMessage(LogLevel.Information, "Starting Monday reminder workflow")]
	partial void LogStartingMondayReminderWorkflow();

	[LoggerMessage(LogLevel.Warning, "No roster found for current week {Date}")]
	partial void LogNoRosterFoundForCurrentWeek(DateTime date);

	[LoggerMessage(LogLevel.Warning, "Chief not found for roster {RosterId}")]
	partial void LogChiefNotFoundForRoster(string rosterId);

	[LoggerMessage(LogLevel.Information, "Handover recommendation sent from {OutgoingChief} to {IncomingChief}")]
	partial void LogHandoverRecommendationSent(string outgoingChief, string incomingChief);

	[LoggerMessage(LogLevel.Warning, "Failed to send handover recommendation: {Error}")]
	partial void LogFailedToSendHandoverRecommendation(string error);

	[LoggerMessage(LogLevel.Warning, "Failed to get members for handover: {Error}")]
	partial void LogFailedToGetMembersForHandover(string error);

	[LoggerMessage(LogLevel.Warning, "Current chief not found for handover: {ChiefId}")]
	partial void LogCurrentChiefNotFoundForHandover(string chiefId);

	[LoggerMessage(LogLevel.Error, "No active team members available")]
	partial void LogNoActiveTeamMembers();

	[LoggerMessage(LogLevel.Information, "Only one active member available, assigning {Chief} as both Chief and Backup")]
	partial void LogOnlyOneMemberAvailable(string chief);
}
