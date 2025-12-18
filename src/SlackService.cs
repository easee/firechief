using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FireChief;

/// <summary>
/// Service for sending notifications to Slack channels with pinning support.
/// </summary>
public sealed partial class SlackService(IOptions<AppConfig> config, ISlackClient client, ILogger<SlackService> logger)
{
	private readonly string _botToken = config.Value.SlackBotToken;
	private readonly string _internalChannelId = config.Value.SlackInternalChannelId;
	private readonly string _publicChannelId = config.Value.SlackPublicChannelId;

	/// <summary>
	/// Sends a message to the internal team channel and returns the message timestamp.
	/// </summary>
	private async Task<Result<string>> SendInternalAsync(string message)
	{
		if (string.IsNullOrWhiteSpace(_botToken))
		{
			LogBotTokenNotConfigured();
			return Result.Failure<string>("Slack bot token not configured");
		}

		try
		{
			var request = new SlackPostRequest(_internalChannelId, message);
			SlackPostMessageResponse response = await client.PostMessageAsync(request, _botToken);

			if (!response.Ok || response.Ts is null)
			{
				LogFailedToSendInternalMessage(response.Error);
				return Result.Failure<string>($"Slack API error: {response.Error ?? "Unknown error"}");
			}

			LogSentInternalMessage();
			return Result.Success(response.Ts);
		}
		catch (Exception ex)
		{
			LogFailedToSendInternalMessage(ex);
			return Result.Failure<string>($"Slack API error: {ex.Message}");
		}
	}

	/// <summary>
	/// Sends a message to the public team channel and returns the message timestamp.
	/// </summary>
	private async Task<Result<string>> SendPublicAsync(string message)
	{
		if (string.IsNullOrWhiteSpace(_botToken))
		{
			LogBotTokenNotConfigured();
			return Result.Failure<string>("Slack bot token not configured");
		}

		try
		{
			var request = new SlackPostRequest(_publicChannelId, message);
			SlackPostMessageResponse response = await client.PostMessageAsync(request, _botToken);

			if (!response.Ok || response.Ts is null)
			{
				LogFailedToSendPublicMessage(response.Error);
				return Result.Failure<string>($"Slack API error: {response.Error ?? "Unknown error"}");
			}

			LogSentPublicMessage();
			return Result.Success(response.Ts);
		}
		catch (Exception ex)
		{
			LogFailedToSendPublicMessage(ex);
			return Result.Failure<string>($"Slack API error: {ex.Message}");
		}
	}

	/// <summary>
	/// Sends a message to the internal team channel and pins it.
	/// </summary>
	public async Task<Result<string>> SendAndPinInternalAsync(string message)
	{
		Result<string> sendResult = await SendInternalAsync(message);
		if (sendResult.IsFailure)
			return sendResult;

		Result pinResult = await PinMessageAsync(_internalChannelId, sendResult.Value);
		if (pinResult.IsFailure)
		{
			LogFailedToPinInternalMessage(pinResult.Error);
			// Still return success with timestamp even if pinning fails
		}

		return sendResult;
	}

	/// <summary>
	/// Sends a message to the public team channel and pins it.
	/// </summary>
	public async Task<Result<string>> SendAndPinPublicAsync(string message)
	{
		Result<string> sendResult = await SendPublicAsync(message);
		if (sendResult.IsFailure)
			return sendResult;

		Result pinResult = await PinMessageAsync(_publicChannelId, sendResult.Value);
		if (pinResult.IsFailure)
		{
			LogFailedToPinPublicMessage(pinResult.Error);
			// Still return success with timestamp even if pinning fails
		}

		return sendResult;
	}

	/// <summary>
	/// Pins a message in the specified channel.
	/// </summary>
	private async Task<Result> PinMessageAsync(string channelId, string messageTs)
	{
		try
		{
			var request = new SlackPinRequest(channelId, messageTs);
			SlackPinResponse response = await client.PinMessageAsync(request, _botToken);

			if (!response.Ok)
			{
				LogFailedToPinMessage(response.Error ?? "Unknown error");
				return Result.Failure($"Slack API error: {response.Error ?? "Unknown error"}");
			}

			LogPinnedMessage(messageTs, channelId);
			return Result.Success();
		}
		catch (Exception ex)
		{
			LogFailedToPinMessage(ex);
			return Result.Failure($"Slack API error: {ex.Message}");
		}
	}

	/// <summary>
	/// Unpins a message in the specified channel.
	/// </summary>
	private async Task<Result> UnpinMessageAsync(string channelId, string messageTs)
	{
		if (string.IsNullOrWhiteSpace(messageTs))
			return Result.Success(); // Nothing to unpin

		try
		{
			var request = new SlackPinRequest(channelId, messageTs);
			SlackPinResponse response = await client.UnpinMessageAsync(request, _botToken);

			if (!response.Ok)
			{
				// Don't fail if message doesn't exist or is already unpinned
				LogFailedToUnpinMessage(messageTs, response.Error ?? "Unknown error");
				return Result.Success();
			}

			LogUnpinnedMessage(messageTs, channelId);
			return Result.Success();
		}
		catch (Exception ex)
		{
			LogFailedToUnpinMessage(ex, messageTs);
			return Result.Success(); // Don't fail the overall workflow if unpinning fails
		}
	}

	/// <summary>
	/// Unpins a message from the internal channel.
	/// </summary>
	public async Task<Result> UnpinInternalMessageAsync(string messageTs) =>
		await UnpinMessageAsync(_internalChannelId, messageTs);

	/// <summary>
	/// Unpins a message from the public channel.
	/// </summary>
	public async Task<Result> UnpinPublicMessageAsync(string messageTs) =>
		await UnpinMessageAsync(_publicChannelId, messageTs);

	/// <summary>
	/// Unpins ALL pinned messages in both internal and public channels.
	/// </summary>
	public async Task<Result<int>> UnpinAllMessagesAsync()
	{
		int unpinnedCount = 0;

		// Get all pinned messages in both channels
		List<Task<Result<List<string>>>> tasks =
		[
			GetPinnedMessageTimestampsAsync(_internalChannelId),
			GetPinnedMessageTimestampsAsync(_publicChannelId)
		];

		await Task.WhenAll(tasks);

		// Collect all timestamps
		List<string> allTimestamps = [];
		foreach (Task<Result<List<string>>> task in tasks)
		{
			Result<List<string>> result = await task;
			if (result.IsSuccess)
			{
				allTimestamps.AddRange(result.Value);
			}
		}

		// Unpin all messages in parallel
		List<Task> unpinTasks = [];
		foreach (string ts in allTimestamps)
		{
			unpinTasks.Add(UnpinMessageAsync(_internalChannelId, ts));
			unpinTasks.Add(UnpinMessageAsync(_publicChannelId, ts));
		}

		if (unpinTasks.Count > 0)
		{
			await Task.WhenAll(unpinTasks);
			unpinnedCount = allTimestamps.Count;
			LogUnpinnedAllMessages(unpinnedCount);
		}

		return Result.Success(unpinnedCount);
	}

	private async Task<Result<List<string>>> GetPinnedMessageTimestampsAsync(string channelId)
	{
		try
		{
			SlackPinsListResponse response = await client.ListPinsAsync(channelId, _botToken);

			if (!response.Ok || response.Items is null)
			{
				LogFailedToListPins(channelId, response.Error ?? "Unknown error");
				return Result.Success(new List<string>());
			}

			var timestamps = response.Items
				.Where(item => item.Message?.Ts is not null)
				.Select(item => item.Message!.Ts!)
				.ToList();

			LogFoundPinnedMessages(timestamps.Count, channelId);
			return Result.Success(timestamps);
		}
		catch (Exception ex)
		{
			LogFailedToListPins(ex, channelId);
			return Result.Success(new List<string>()); // Don't fail the workflow
		}
	}
}
