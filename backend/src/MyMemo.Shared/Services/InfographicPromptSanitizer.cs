using System.ClientModel;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace MyMemo.Shared.Services;

public sealed class InfographicPromptSanitizer : IInfographicPromptSanitizer
{
    private readonly Lazy<ChatClient> _chatClient;
    private readonly ILogger<InfographicPromptSanitizer> _logger;

    private const string SystemPrompt = """
        You are an infographic prompt generator. Given meeting memo content, produce a single image-generation prompt describing a professional infographic layout.

        RULES:
        - Extract only key themes, structure, and data points from the memo
        - Output a single image-generation prompt — no preamble, no markdown, no explanation
        - Describe a professional infographic layout using abstract visual language: shapes, icons, color schemes, typography hierarchy, flow arrows, chart types
        - Strip ALL personally identifiable information: names, CPR/CVR numbers, addresses, phone numbers, email addresses
        - Strip ALL sensitive content: medical diagnoses, legal accusations, political statements, case-specific details
        - Replace specific entities with generic equivalents (e.g., a specific municipality → "a municipality", a person's name → "[person icon]")
        - Keep the output under 500 words
        - Target style: clean, modern, corporate infographic, flat design, muted professional color palette
        - Write the infographic content descriptions in the same language as the memo
        """;

    public InfographicPromptSanitizer(
        IOptions<AzureOpenAIOptions> options,
        ILogger<InfographicPromptSanitizer> logger)
    {
        _logger = logger;
        _chatClient = new Lazy<ChatClient>(() =>
        {
            var credential = new ApiKeyCredential(options.Value.ApiKey);
            var client = new AzureOpenAIClient(new Uri(options.Value.Endpoint), credential);
            return client.GetChatClient(options.Value.GptDeployment);
        });
    }

    public async Task<string> SanitizeAsync(string memoContent, CancellationToken ct = default)
    {
        var chatClient = _chatClient.Value;

        ChatMessage[] messages =
        [
            new SystemChatMessage(SystemPrompt),
            new UserChatMessage(memoContent)
        ];

        var options = new ChatCompletionOptions
        {
            Temperature = 0.3f,
            MaxOutputTokenCount = 600,
        };

        var result = await chatClient.CompleteChatAsync(messages, options, ct);
        var sanitizedPrompt = result.Value.Content[0].Text.Trim();

        _logger.LogDebug(
            "Sanitized infographic prompt: original length {OriginalLength}, sanitized length {SanitizedLength}",
            memoContent.Length, sanitizedPrompt.Length);

        _logger.LogInformation("Sanitized infographic prompt: {SanitizedPrompt}", sanitizedPrompt);

        return sanitizedPrompt;
    }
}
