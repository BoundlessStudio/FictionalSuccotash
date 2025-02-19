using System.Text.Json.Serialization;

namespace FictionalSuccotash.Models;

public class SummaryDto
{
  [JsonPropertyName("attempts")]
  public List<int> Attempts { get; set; } = new List<int>();

  [JsonPropertyName("successes")]
  public List<int> Successes { get; set; } = new List<int>();
}