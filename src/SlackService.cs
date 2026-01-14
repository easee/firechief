using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FireChief;

/// <summary>
/// Service for sending notifications to Slack channels with topic setting support.
/// </summary>
public sealed partial class SlackService(IOptions<AppConfig> config, ISlackClient client, ILogger<SlackService> logger)
{
	private readonly string _botToken = config.Value.SlackBotToken;
	private readonly string _internalChannelId = config.Value.SlackInternalChannelId;
	private readonly string _publicChannelId = config.Value.SlackPublicChannelId;

	/// <summary>
	/// Sends a message to the internal team channel and returns the message timestamp.
	/// </summary>
	public async Task<Result<string>> SendInternalAsync(string message)
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
	/// Sends a message to the internal team channel and sets the channel topic.
	/// </summary>
	public async Task<Result<string>> SendAndSetInternalTopicAsync(string message, string topic)
	{
		Result<string> sendResult = await SendInternalAsync(message);

		if (sendResult.IsFailure)
			return sendResult;

		// Set topic (don't fail if this doesn't work)
		await SetChannelTopicAsync(_internalChannelId, topic);

		return sendResult;
	}

	/// <summary>
	/// Sends a message to the public team channel and sets the channel topic.
	/// </summary>
	public async Task<Result<string>> SendAndSetPublicTopicAsync(string message, string topic)
	{
		Result<string> sendResult = await SendPublicAsync(message);

		if (sendResult.IsFailure)
			return sendResult;

		// Set topic (don't fail if this doesn't work)
		await SetChannelTopicAsync(_publicChannelId, topic);

		return sendResult;
	}

	/// <summary>
	/// Sets the topic for a channel.
	/// </summary>
	private async Task SetChannelTopicAsync(string channelId, string topic)
	{
		try
		{
			var request = new SlackSetTopicRequest(channelId, topic);
			SlackTopicResponse response = await client.SetTopicAsync(request, _botToken);

			if (!response.Ok)
			{
				LogFailedToSetTopic(channelId, response.Error ?? "Unknown error");
				Result.Failure($"Slack API error: {response.Error ?? "Unknown error"}");
				return;
			}

			LogSetTopic(channelId, topic);
			Result.Success();
		}
		catch (Exception ex)
		{
			LogFailedToSetTopic(ex, channelId);
			Result.Success();
		}
	}
}
