using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenAI.Chat;

var builder = FunctionsApplication.CreateBuilder(args);
builder.ConfigureFunctionsWebApplication();
builder.Services.AddSingleton(new ChatClient("gpt-4o-mini", Environment.GetEnvironmentVariable("OPENAI_API_KEY")));
builder.Build().Run();
