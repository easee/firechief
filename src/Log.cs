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

	[LoggerMessage(LogLevel.Warning, "Failed to pin internal message: {Error}")]
	partial void LogFailedToPinInternalMessage(string error);

	[LoggerMessage(LogLevel.Warning, "Failed to pin public message: {Error}")]
	partial void LogFailedToPinPublicMessage(string error);

	[LoggerMessage(LogLevel.Error, "Failed to pin message: {Error}")]
	partial void LogFailedToPinMessage(string error);

	[LoggerMessage(LogLevel.Information, "Pinned message {MessageTs} in {ChannelId}")]
	partial void LogPinnedMessage(string messageTs, string channelId);

	[LoggerMessage(LogLevel.Error, "Failed to pin message")]
	partial void LogFailedToPinMessage(Exception exception);

	[LoggerMessage(LogLevel.Warning, "Failed to unpin message {MessageTs}: {Error}")]
	partial void LogFailedToUnpinMessage(string messageTs, string error);

	[LoggerMessage(LogLevel.Information, "Unpinned message {MessageTs} in {ChannelId}")]
	partial void LogUnpinnedMessage(string messageTs, string channelId);

	[LoggerMessage(LogLevel.Warning, "Failed to unpin message {MessageTs}")]
	partial void LogFailedToUnpinMessage(Exception exception, string messageTs);

	[LoggerMessage(LogLevel.Information, "Unpinned {Count} messages")]
	partial void LogUnpinnedAllMessages(int count);

	[LoggerMessage(LogLevel.Warning, "Failed to list pins for {ChannelId}: {Error}")]
	partial void LogFailedToListPins(string channelId, string error);

	[LoggerMessage(LogLevel.Information, "Found {Count} pinned messages in {ChannelId}")]
	partial void LogFoundPinnedMessages(int count, string channelId);

	[LoggerMessage(LogLevel.Error, "Failed to list pins for {ChannelId}")]
	partial void LogFailedToListPins(Exception exception, string channelId);
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

	[LoggerMessage(LogLevel.Information, "Starting Friday reminder workflow")]
	partial void LogStartingFridayReminderWorkflow();

	[LoggerMessage(LogLevel.Warning, "No roster found for current week {Date}")]
	partial void LogNoRosterFoundForCurrentWeek(DateTime date);

	[LoggerMessage(LogLevel.Warning, "Chief not found for roster {RosterId}")]
	partial void LogChiefNotFoundForRoster(string rosterId);

	[LoggerMessage(LogLevel.Information, "Unpinned all previous messages before creating new assignment")]
	partial void LogUnpinnedAllPreviousMessages();

	[LoggerMessage(LogLevel.Warning, "Failed to unpin all messages: {Error}")]
	partial void LogFailedToUnpinAllMessages(string error);

	[LoggerMessage(LogLevel.Error, "No active team members available")]
	partial void LogNoActiveTeamMembers();

	[LoggerMessage(LogLevel.Information, "Only one active member available, assigning {Chief} as both Chief and Backup")]
	partial void LogOnlyOneMemberAvailable(string chief);
}
