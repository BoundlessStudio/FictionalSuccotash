using System.Text.Json.Serialization;

namespace FictionalSuccotash.Models;

public class PinInputDto
{

  [JsonPropertyName("level")]
  public int Level { get; set; }

  [JsonPropertyName("code")]
  public string Code { get; set; } = string.Empty;

}
