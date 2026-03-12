using System.ClientModel;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMemo.Shared.Exceptions;
using OpenAI.Images;

namespace MyMemo.Shared.Services;

public sealed class InfographicService : IInfographicService
{
    private readonly Lazy<ImageClient> _imageClient;
    private readonly string _imageDeployment;
    private readonly IInfographicPromptSanitizer _sanitizer;
    private readonly ILogger<InfographicService> _logger;
    private static readonly HttpClient s_httpClient = new();

    public InfographicService(
        IOptions<AzureOpenAIOptions> options,
        IInfographicPromptSanitizer sanitizer,
        ILogger<InfographicService> logger)
    {
        _imageDeployment = options.Value.ImageDeployment;
        _sanitizer = sanitizer;
        _logger = logger;
        _imageClient = new Lazy<ImageClient>(() =>
        {
            var credential = new ApiKeyCredential(options.Value.ApiKey);
            var client = new AzureOpenAIClient(new Uri(options.Value.Endpoint), credential);
            return client.GetImageClient(options.Value.ImageDeployment);
        });
    }

    public async Task<InfographicResult> GenerateAsync(string sessionId, string memoContent, string outputMode)
    {
        var imageClient = _imageClient.Value;

        var sanitizedContent = await _sanitizer.SanitizeAsync(memoContent);
        var prompt = BuildPrompt(sanitizedContent, outputMode);

        var options = new ImageGenerationOptions
        {
            Size = new GeneratedImageSize(1024, 1536),
            Quality = new GeneratedImageQuality("high"),
        };

        try
        {
            var result = await imageClient.GenerateImageAsync(prompt, options);
            var image = result.Value;

            byte[] imageBytes;
            if (image.ImageUri is not null)
            {
                imageBytes = await s_httpClient.GetByteArrayAsync(image.ImageUri);
            }
            else if (image.ImageBytes is not null)
            {
                imageBytes = image.ImageBytes.ToArray();
            }
            else
            {
                throw new InvalidOperationException("Image generation returned neither a URI nor binary data.");
            }

            var base64 = Convert.ToBase64String(imageBytes);

            return new InfographicResult(
                ImageBase64: base64,
                ModelUsed: _imageDeployment,
                PromptTokens: null,
                CompletionTokens: null);
        }
        catch (ClientResultException ex) when (
            ex.Status == 400 && ex.Message.Contains("moderation_blocked"))
        {
            _logger.LogWarning(
                "Moderation blocked after sanitization for session {SessionId}. Sanitized prompt: {Prompt}",
                sessionId, sanitizedContent);

            throw new InfographicModerationException(
                sessionId,
                "Content could not be converted to a safe infographic prompt",
                sanitizedContent,
                ex);
        }
    }

    private static string BuildPrompt(string memoContent, string outputMode)
    {
        var contentGuidance = outputMode switch
        {
            "summary" => "Highlight key points, decisions, action items, and next steps.",
            "product-planning" => "Show features, priorities (must-have vs nice-to-have), risks, and next steps.",
            _ => "Extract main topics, key quotes, and structure into digestible sections."
        };

        return $"""
            Create a professional, visually appealing infographic in portrait layout based on the following meeting memo.

            DESIGN REQUIREMENTS:
            - Clean, modern corporate design with a white background
            - Professional color palette: primary blue (#2563EB), purple (#7C3AED), green accent (#059669), dark text (#1E293B)
            - Clear visual hierarchy with a bold title at the top
            - Use card-like sections with rounded corners for each topic
            - Include simple geometric icons/shapes to represent concepts
            - Use bullet points for lists
            - Maximum 6-8 content sections to keep it readable
            - All text must be clearly legible
            - Write all content in the same language as the memo (typically Danish)

            CONTENT GUIDANCE:
            {contentGuidance}

            MEMO CONTENT:
            {memoContent}
            """;
    }
}
