using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Configurations;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.OpenApi.Models;

namespace FictionalSuccotash;

internal class OpenApiConfigurationOptions : DefaultOpenApiConfigurationOptions
{
  public override OpenApiInfo Info { get; set; } = new OpenApiInfo
  {
    Version = "0.1.0",
    Title = "guards.it",
    Description = "",
  };

  public override bool ForceHttps { get; set; } = true;

  public override OpenApiVersionType OpenApiVersion { get; set; } = OpenApiVersionType.V3;
}
