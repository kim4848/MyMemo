using System.ClientModel;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;
using OpenAI.Images;

namespace MyMemo.Shared.Services;

public sealed class InfographicService : IInfographicService
{
    private readonly Lazy<ImageClient> _imageClient;
    private readonly string _imageDeployment;

    public InfographicService(IOptions<AzureOpenAIOptions> options)
    {
        _imageDeployment = options.Value.ImageDeployment;
        _imageClient = new Lazy<ImageClient>(() =>
        {
            var credential = new ApiKeyCredential(options.Value.ApiKey);
            var client = new AzureOpenAIClient(new Uri(options.Value.Endpoint), credential);
            return client.GetImageClient(options.Value.ImageDeployment);
        });
    }

    public async Task<InfographicResult> GenerateAsync(string memoContent, string outputMode)
    {
        var imageClient = _imageClient.Value;

        var prompt = BuildPrompt(memoContent, outputMode);

        var imageOptions = new ImageGenerationOptions
        {
            Size = GeneratedImageSize.W1024xH1536,
            Quality = GeneratedImageQuality.High,
            ResponseFormat = GeneratedImageFormat.Bytes,
        };

        var response = await imageClient.GenerateImageAsync(prompt, imageOptions);
        var generatedImage = response.Value;

        var imageBytes = generatedImage.ImageBytes
            ?? throw new InvalidOperationException("Image generation returned no image bytes.");

        var base64 = Convert.ToBase64String(imageBytes.ToArray());

        return new InfographicResult(
            ImageBase64: base64,
            ImageFormat: "png",
            ModelUsed: _imageDeployment);
    }

    private static string BuildPrompt(string memoContent, string outputMode)
    {
        var contentSection = outputMode switch
        {
            "summary" => "This is a meeting summary memo. Highlight: key points, decisions made, action items, and next steps.",
            "product-planning" => "This is a product planning memo. Show: features, priorities (must-have vs nice-to-have), risks, and next steps.",
            _ => "This is a full meeting transcription memo. Extract: main topics, key quotes, and structure into digestible sections.",
        };

        return $"""
            Create a professional infographic that visually presents the following meeting memo content.

            CONTENT TYPE: {contentSection}

            MEMO CONTENT:
            {memoContent}

            VISUAL STYLE:
            - Clean, professional vertical layout
            - Use a clear visual hierarchy with a title at the top, followed by organized sections
            - Use icons, arrows, and visual separators between sections
            - Professional color palette: blues, purples, greens on a light background
            - Use bullet points and numbered lists where appropriate
            - Maximum 6-8 content sections to keep it readable
            - All text must be in the same language as the memo content
            - High quality, sharp text that is easy to read
            - Include simple geometric icons/shapes to represent concepts
            """;
    }
}
