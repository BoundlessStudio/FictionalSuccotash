using FictionalSuccotash.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Client.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace FictionalSuccotash.Functions;

public class GuardFunction
{
  private readonly ILogger<GuardFunction> _logger;
  private readonly IMemoryCache _cache;
  private readonly OpenAIClient _client;
  private static readonly ConcurrentDictionary<string, List<Level>> _store = new ConcurrentDictionary<string, List<Level>>();
  public GuardFunction(ILogger<GuardFunction> logger, IMemoryCache cache, OpenAIClient client)
  {
    _logger = logger;
    _cache = cache;
    _client = client;
  }

  [Function("Start")]
  public async Task<IActionResult> Start([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req)
  {
    var ip = req.HttpContext?.Connection?.RemoteIpAddress?.ToString();
    if (ip is null)
      return new BadRequestObjectResult(new { message = "Could not determine IP address." });

    var dto = await req.ReadFromJsonAsync<StartInputDto>();
    if (dto is null)
      return new BadRequestObjectResult(new { message = "Missing Dto" });

    var levels = LevelGenerator.GenerateLevels(dto.Difficulty);
    _store.TryRemove(ip, out List<Level>? existing);
    _store.TryAdd(ip, levels);

    _logger.LogInformation($"Starting new Session for {ip}.");

    var result = new StartOutputDto()
    {
      Hint = levels[3].Code
    };
    return new OkObjectResult(result);
  }

  [Function("Pin")]
  public async Task<IActionResult> Pin([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req, [DurableClient] DurableTaskClient client)
  {
    var ip = req.HttpContext?.Connection?.RemoteIpAddress?.ToString();
    if (ip is null)
      return new BadRequestObjectResult(new { message = "Could not determine IP address." });

    var dto = await req.ReadFromJsonAsync<PinInputDto>();
    if (dto is null)
      return new BadRequestObjectResult(new { message = "Missing Dto" });

    _logger.LogInformation($"Pin request: IP='{ip}', level='{dto.Level}', code='{dto.Code}'.");

    _store.TryGetValue(ip, out var levels);
    if (levels is null)
      return new NotFoundObjectResult(new { message = "No active session" });

    var level = levels.ElementAtOrDefault(dto.Level - 1);
    if (level is null)
      return new NotFoundObjectResult(new { message = "Unknown Level" });

    await client.Entities.SignalEntityAsync(new(nameof(Counter), $"attempts-{dto.Level}"), "Increment");

    level.Success = level.Code == dto.Code;

    if (level.Success) await client.Entities.SignalEntityAsync(new(nameof(Counter), $"successes-{dto.Level}"), "Increment");

    var result = new PinOutputDto()
    {
      Success = level.Success,
    };
    return new OkObjectResult(result);
  }

  [Function("Chat")]
  public async Task<IActionResult> Chat([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req)
  {
    var ip = req.HttpContext?.Connection?.RemoteIpAddress?.ToString();
    if (ip is null)
      return new BadRequestObjectResult(new { message = "Could not determine IP address." });

    var dto = await req.ReadFromJsonAsync<ChatInputDto>();
    if (dto is null)
      return new BadRequestObjectResult(new { message = "Missing Dto" });

    _store.TryGetValue(ip, out var levels);
    if (levels is null)
      return new NotFoundObjectResult(new { message = "No active session" });

    var level = levels.ElementAtOrDefault(dto.Level - 1);
    if (level is null)
      return new NotFoundObjectResult(new { message = "Unknown Level" });

    var model = LevelGenerator.GetModel(dto.Level);
    var prompt = LevelGenerator.GetPrompt(dto.Level, level.Code);
    var messages = new List<ChatMessage>()
    {
      ChatMessage.CreateSystemMessage(prompt)
    };
    foreach (var msg in dto.Messages)
    {
      switch (msg.Role.ToLowerInvariant())
      {
        case "user":
          messages.Add(ChatMessage.CreateUserMessage(msg.Content));
          break;
        case "assistant":
          messages.Add(ChatMessage.CreateAssistantMessage(msg.Content));
          break;
        default:
          break;
      }
    }

    var options = new ChatCompletionOptions()
    {
      EndUserId = ip,
      MaxOutputTokenCount = 512,
    };

    
    var chat = _client.GetChatClient(model);
    ChatCompletion response = await chat.CompleteChatAsync(messages, options);
    var assistant = response?.Content.FirstOrDefault()?.Text ?? string.Empty;

    if (dto.Level == 9)
    {
      assistant.Replace(level.Code, "****");
    }

    var result = new ChatOutputDto()
    {
      Response = assistant,
    };
    return new OkObjectResult(result);
  }

  [Function("Summary")]
  public async Task<IActionResult> Summary([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req, [DurableClient] DurableTaskClient client)
  {
    if(_cache.TryGetValue("Summary", out SummaryDto? dto))
    {
      return new OkObjectResult(dto);
    }

    var attemptsTasks = new List<Task<EntityMetadata<int>?>>();
    var successesTasks = new List<Task<EntityMetadata<int>?>>();
    for (int i = 1; i <= 10; i++)
    {
      attemptsTasks.Add(client.Entities.GetEntityAsync<int>(new(nameof(Counter), $"attempts-{i}")));
      successesTasks.Add(client.Entities.GetEntityAsync<int>(new(nameof(Counter), $"successes-{i}")));
    }

    var attemptsCollection = await Task.WhenAll(attemptsTasks);
    var successesCollection = await Task.WhenAll(successesTasks);

    var result = new SummaryDto()
    {
      Attempts = attemptsCollection.Select(_ => _?.State ?? 0).ToList(),
      Successes = successesCollection.Select(_ => _?.State ?? 0).ToList()
    };

    _cache.Set("Summary", result, new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromMinutes(5)));
    return new OkObjectResult(result);
  }

  [Function("Session")]
  public IActionResult Session([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
  {
    var ip = req.HttpContext?.Connection?.RemoteIpAddress?.ToString();
    if (ip is null)
      return new BadRequestObjectResult(new { message = "Could not determine IP address." });

    _store.TryGetValue(ip, out var levels);
    if (levels is null)
      return new NotFoundObjectResult(new { message = "No active session" });

    var codes = levels.Select(l => l.Code).ToList();

    return new OkObjectResult(codes);
  }

}