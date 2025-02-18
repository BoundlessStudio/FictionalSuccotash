using OpenAI.Chat;
using System.Text.Json.Serialization;

namespace FictionalSuccotash.Models;

public class ChatInputDto
{

  [JsonPropertyName("level")]
  public int Level { get; set; }

  [JsonPropertyName("messages")]
  public List<ChatMessageDto> Messages { get; set; } = new List<ChatMessageDto>();
}

public class ChatMessageDto
{
  [JsonPropertyName("role")]
  public string Role { get; set; } = string.Empty;

  [JsonPropertyName("content")]
  public string Content { get; set; } = string.Empty;
}