using FictionalSuccotash.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using System.Collections.Concurrent;
using System.Net;

namespace FictionalSuccotash.Functions;

public class GuardFunction
{
  private readonly ILogger<GuardFunction> _logger;
  private readonly ChatClient _client;
  private static readonly ConcurrentDictionary<string, List<Level>> _store = new ConcurrentDictionary<string, List<Level>>();
  public GuardFunction(ILogger<GuardFunction> logger, ChatClient client)
  {
    _logger = logger;
    _client = client;
  }

  [Function("Start")]
  [OpenApiOperation(operationId: "Start",
            Summary = "Starts a new game session",
            Description = "Initializes a game session and returns a hint.")]
  [OpenApiRequestBody("application/json", typeof(StartInputDto), Required = true,
            Description = "Configuration for starting a new session, such as difficulty.")]
  [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json",
            bodyType: typeof(StartOutputDto),
            Description = "Successfully started a new session, with a hint.")]
  [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest,
            Description = "Could not determine IP address or missing StartInputDto.")]
  public async Task<IActionResult> Start([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req)
  {
    var ip = req.HttpContext?.Connection?.RemoteIpAddress?.ToString();
    if (ip is null)
      return new BadRequestObjectResult(new { message = "Could not determine IP address." });

    var dto = await req.ReadFromJsonAsync<StartInputDto>();
    if (dto is null)
      return new BadRequestObjectResult(new { message = "Missing Dto" });

    var levels = LevelGenerator.Generate(dto.Difficulty);
    _store.TryAdd(ip, levels);

    _logger.LogInformation($"Starting new Session for {ip}.");

    var result = new StartOutputDto()
    {
      Hint = levels[3].Code
    };
    return new OkObjectResult(result);
  }

  [Function("Pin")]
  [OpenApiOperation(operationId: "Pin" ,
            Summary = "Verifies a level's security code",
            Description = "Checks whether the provided code matches the level requirement.")]
  [OpenApiRequestBody("application/json", typeof(PinInputDto), Required = true,
            Description = "Level index and attempted code.")]
  [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json",
            bodyType: typeof(PinOutputDto),
            Description = "Returns whether the pin was successful and if attempts exceeded the limit.")]
  [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest,
            Description = "IP address not determined or missing request DTO.")]
  [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound,
            Description = "Either the session was not found or the level does not exist.")]
  public async Task<IActionResult> Pin([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req)
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

    level.Success = level.Code == dto.Code;

    var result = new PinOutputDto()
    {
      Success = level.Success,
    };
    return new OkObjectResult(result);
  }

  [Function("Chat")]
  [OpenApiOperation(operationId: "Chat",
            Summary = "AI chat for a given level",
            Description = "Submits a user prompt to an AI bot, guided by the specified level's instructions.")]
  [OpenApiRequestBody("application/json", typeof(ChatInputDto), Required = true,
            Description = "Includes the level index and the user prompt.")]
  [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json",
            bodyType: typeof(ChatOutputDto),
            Description = "AI-generated response to the given prompt.")]
  [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest,
            Description = "IP address not determined or missing request DTO.")]
  [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound,
            Description = "No active session found or invalid level specified.")]
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

    var messages = new List<ChatMessage>()
    {
      ChatMessage.CreateSystemMessage(level.Prompt)
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

    ChatCompletion response = await _client.CompleteChatAsync(messages, options);
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


}
