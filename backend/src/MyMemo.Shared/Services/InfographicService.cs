using System.ClientModel;
using System.Text;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace MyMemo.Shared.Services;

public sealed class InfographicService : IInfographicService
{
    private readonly Lazy<ChatClient> _chatClient;
    private readonly string _gptDeployment;

    public InfographicService(IOptions<AzureOpenAIOptions> options)
    {
        _gptDeployment = options.Value.GptDeployment;
        _chatClient = new Lazy<ChatClient>(() =>
        {
            var credential = new ApiKeyCredential(options.Value.ApiKey);
            var client = new AzureOpenAIClient(new Uri(options.Value.Endpoint), credential);
            return client.GetChatClient(options.Value.GptDeployment);
        });
    }

    private const string SystemPrompt = """
        You are a professional infographic designer. Given memo content from a meeting transcription,
        generate a visually appealing SVG infographic that presents the key information.

        CRITICAL SVG REQUIREMENTS:
        - Output ONLY valid SVG markup, nothing else — no markdown, no code fences, no explanation
        - The SVG must start with <svg and end with </svg>
        - Use viewBox="0 0 800 1100" for a portrait layout
        - Use width="800" height="1100"
        - Use a clean, modern design with a white (#FFFFFF) background
        - Use a professional color palette: primary (#2563EB), secondary (#7C3AED), accent (#059669), dark (#1E293B), muted (#64748B)
        - Include a title section at the top with large bold text
        - Use rounded rectangles (rx="12") as card containers for sections
        - Use clear visual hierarchy with font sizes: title 28px, section headers 18px, body text 14px
        - Use the font-family="Inter, system-ui, sans-serif"
        - Include simple geometric icons/shapes to represent concepts (circles, arrows, checkmarks)
        - Wrap long text using multiple <tspan> elements with dy="1.3em" and appropriate x positioning
        - Each line of text should be max ~60 characters wide
        - Leave comfortable padding (20px) inside card containers
        - Space sections vertically with 16-20px gaps

        CONTENT EXTRACTION:
        - For "summary" memos: highlight key points, decisions, action items, and next steps
        - For "full" memos: extract main topics, key quotes, and structure into digestible sections
        - For "product-planning" memos: show features, priorities (must-have vs nice-to-have), risks, and next steps
        - Always include a header with the memo title/topic
        - Use bullet points (• character) for lists
        - Maximum 6-8 content sections to keep it readable
        - Write all content in the same language as the memo (typically Danish)
        """;

    public async Task<InfographicResult> GenerateAsync(string memoContent, string outputMode)
    {
        var chatClient = _chatClient.Value;

        var userPrompt = $"Create an infographic for this {outputMode} memo:\n\n{memoContent}";

        ChatMessage[] messages =
        [
            new SystemChatMessage(SystemPrompt),
            new UserChatMessage(userPrompt)
        ];

        var contentBuilder = new StringBuilder();
        int promptTokens = 0;
        int completionTokens = 0;

        await foreach (var update in chatClient.CompleteChatStreamingAsync(messages))
        {
            foreach (var part in update.ContentUpdate)
            {
                contentBuilder.Append(part.Text);
            }

            if (update.Usage is not null)
            {
                promptTokens = update.Usage.InputTokenCount;
                completionTokens = update.Usage.OutputTokenCount;
            }
        }

        var svg = contentBuilder.ToString().Trim();

        // Strip any markdown code fences if the model wraps the SVG
        if (svg.StartsWith("```"))
        {
            var firstNewline = svg.IndexOf('\n');
            if (firstNewline >= 0)
                svg = svg[(firstNewline + 1)..];
            if (svg.EndsWith("```"))
                svg = svg[..^3].TrimEnd();
        }

        return new InfographicResult(
            SvgContent: svg,
            ModelUsed: _gptDeployment,
            PromptTokens: promptTokens,
            CompletionTokens: completionTokens);
    }
}
