using System.Text.Json.Serialization;

namespace FictionalSuccotash.Models;

public class ChatOutputDto
{
  [JsonPropertyName("response")]
  public string Response { get; set; } = string.Empty;

}
