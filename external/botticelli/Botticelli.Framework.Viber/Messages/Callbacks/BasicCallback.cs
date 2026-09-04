using System.Text.Json.Serialization;

namespace Botticelli.Framework.Viber.Messages.Callbacks;

public abstract class BasicCallback
{
    [JsonPropertyName("event")]
    public virtual string Event { get; set; }

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    [JsonPropertyName("message_token")]
    public long MessageToken { get; set; }
}