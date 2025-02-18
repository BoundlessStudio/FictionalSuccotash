using System.Text.Json.Serialization;

namespace FictionalSuccotash.Models;

public class StartOutputDto
{
  [JsonPropertyName("hint")]
  public string Hint { get; set; } = string.Empty;

}
