using System.Text.Json.Serialization;

namespace FictionalSuccotash.Models;

public class StartInputDto
{
  [JsonPropertyName("difficulty")]
  public int Difficulty { get; set; } = 1;

}
