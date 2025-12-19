using System.Text.Json.Serialization;
using Refit;

namespace FireChief;

public interface ISlackClient
{
	[Post("/chat.postMessage")]
	[Headers("Authorization: Bearer")]
	Task<SlackPostMessageResponse> PostMessageAsync([Body] SlackPostRequest request, [Authorize] string token);

	[Post("/pins.add")]
	[Headers("Authorization: Bearer")]
	Task<SlackPinResponse> PinMessageAsync([Body] SlackPinRequest request, [Authorize] string token);

	[Post("/pins.remove")]
	[Headers("Authorization: Bearer")]
	Task<SlackPinResponse> UnpinMessageAsync([Body] SlackPinRequest request, [Authorize] string token);

	[Get("/pins.list")]
	[Headers("Authorization: Bearer")]
	Task<SlackPinsListResponse> ListPinsAsync([Query] string channel, [Authorize] string token);
}

public record SlackPostRequest(
	[property: JsonPropertyName("channel")]
	string Channel,
	[property: JsonPropertyName("text")] string Text
);

public record SlackPostMessageResponse(
	[property: JsonPropertyName("ok")] bool Ok,
	[property: JsonPropertyName("ts")] string? Ts,
	[property: JsonPropertyName("error")] string? Error
);

public record SlackPinRequest(
	[property: JsonPropertyName("channel")]
	string Channel,
	[property: JsonPropertyName("timestamp")]
	string Timestamp
);

public record SlackPinResponse(
	[property: JsonPropertyName("ok")] bool Ok,
	[property: JsonPropertyName("error")] string? Error
);

public record SlackPinsListResponse(
	[property: JsonPropertyName("ok")] bool Ok,
	[property: JsonPropertyName("items")] List<SlackPinItem>? Items,
	[property: JsonPropertyName("error")] string? Error
);

public record SlackPinItem(
	[property: JsonPropertyName("message")]
	SlackPinMessage? Message
);

public record SlackPinMessage(
	[property: JsonPropertyName("ts")] string? Ts
);
