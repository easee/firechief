namespace FireChief;

/// <summary>
/// Represents a team member eligible for Fire Chief rotation.
/// </summary>
public sealed record TeamMember(
	string Id,
	string Name,
	string SlackId,
	DateTime? LastChiefDate,
	bool IsActive,
	bool IsVolunteer
);

/// <summary>
/// Represents a weekly Fire Chief roster entry.
/// </summary>
public sealed record RosterEntry(
	string Id,
	DateTime WeekStart,
	string ChiefId,
	string BackupId,
	string Status = "Planned",
	string? PublicMessageTs = null,
	string? InternalMessageTs = null
);

/// <summary>
/// Application configuration for FireChief bot.
/// Enforces required properties for critical settings.
/// </summary>
public sealed record AppConfig
{
	public required string NotionToken { get; init; }
	public required string TeamDatabaseId { get; init; }
	public required string RosterDatabaseId { get; init; }
	public required string SlackBotToken { get; init; }
	public required string SlackInternalChannelId { get; init; }
	public required string SlackPublicChannelId { get; init; }
}

/// <summary>
/// Represents the result of a Fire Chief assignment.
/// </summary>
public sealed record AssignmentResult(TeamMember Chief, TeamMember Backup, DateTime WeekStart)
{
	public string FormatPublicAnnouncement()
	{
		string backupLine = Chief.Id == Backup.Id
			? ""
			: $"\n🛡️ Backup: <@{Backup.SlackId}>";

		return $"""
			🔥 This week's Fire Chief: <@{Chief.SlackId}>{backupLine}

			The Fire Chief is your go-to person for support requests this week!
			""";
	}

	public string FormatInternalNotification()
	{
		string weekDate = WeekStart.ToString("MMM dd, yyyy");
		string backupLine = Chief.Id == Backup.Id
			? "🛡️ Backup: _No backup available (only one active member)_"
			: $"🛡️ Backup: <@{Backup.SlackId}>";

		return $"""
			🔥 Fire Chief Assignment – Week of {weekDate}

			🧑‍🚒 Chief: <@{Chief.SlackId}>
			{backupLine}

			Remember to:
			- Monitor #team-software for requests
			- Triage and create Linear issues
			- Shield the team from interruptions
			- Prepare handover for next Friday
			""";
	}
}

/// <summary>
/// Discriminated union for assignment operation results.
/// </summary>
public abstract record AssignmentOutcome;

public sealed record AssignmentCreated(AssignmentResult Assignment) : AssignmentOutcome;

public sealed record AssignmentAlreadyExists(DateTime WeekStart) : AssignmentOutcome;

public sealed record InsufficientCandidates(int AvailableCount) : AssignmentOutcome;
