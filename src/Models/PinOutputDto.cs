using System.Text.Json.Serialization;

namespace FictionalSuccotash.Models;

public class PinOutputDto
{
  [JsonPropertyName("success")]
  public bool Success { get; set; }
}
