using System.ClientModel;
using System.Text;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace MyMemo.Shared.Services;

public sealed class MemoGeneratorService : IMemoGeneratorService
{
    private readonly Lazy<ChatClient> _chatClient;
    private readonly string _gptDeployment;

    public MemoGeneratorService(IOptions<AzureOpenAIOptions> options)
    {
        _gptDeployment = options.Value.GptDeployment;
        _chatClient = new Lazy<ChatClient>(() =>
        {
            var credential = new ApiKeyCredential(options.Value.ApiKey);
            var client = new AzureOpenAIClient(new Uri(options.Value.Endpoint), credential);
            return client.GetChatClient(options.Value.GptDeployment);
        });
    }
    private const string FullModePrompt = """
        Du er en professionel dansk transskribent. Renskiv følgende rå transkription.

        Regler:
        - Ret grammatik og stavefejl
        - Tilføj korrekt tegnsætning
        - Bevar talerens oprindelige ordvalg og tone
        - Strukturer i afsnit med logiske pauser
        - Marker tydeligt hvis noget er uhørbart: [uhørbart]
        - Bevar tidsstempler som sektion-markører
        - Output på dansk
        - Fjern gentagelser og fyldord (øh, altså, ikke?) — bevar kun meningsfuldt indhold
        """;

    private const string SummaryModePrompt = """
        Du er en professionel dansk mødesekretær. Lav et struktureret referat af følgende transkription.

        Format:
        - Titel/emne (udledt fra indhold)
        - Dato og varighed
        - Deltagere (hvis nævnt)
        - Hovedpunkter (kort, præcist)
        - Beslutninger
        - Action items (hvem, hvad, hvornår)
        - Næste skridt

        Regler:
        - Skriv på dansk
        - Vær kortfattet — maks 500 ord
        - Brug korte sætninger og stikord
        - Marker usikre punkter med [?]
        """;

    private const string ProductPlanningModePrompt = """
        Du er en erfaren dansk produktchef. Analysér følgende transkription og lav en struktureret produktplanlægning.

        Format:
        - Titel/emne (udledt fra indhold)
        - Brugerproblemer og behov (identificeret fra samtalen)
        - Foreslåede features/løsninger
        - Prioritering (must-have, nice-to-have)
        - Afhængigheder og risici
        - Næste skridt
        - Åbne spørgsmål

        Regler:
        - Skriv på dansk
        - Vær kortfattet — maks 600 ord
        - Fokusér på brugerværdi og forretningsimpact
        - Vær konkret med prioriteringer
        - Marker usikre punkter med [?]
        """;

    public async Task<MemoResult> GenerateAsync(string fullTranscription, string outputMode, string? context = null)
    {
        var chatClient = _chatClient.Value;

        var systemPrompt = outputMode switch
        {
            "summary" => SummaryModePrompt,
            "product-planning" => ProductPlanningModePrompt,
            _ => FullModePrompt,
        };

        if (!string.IsNullOrWhiteSpace(context))
        {
            systemPrompt += $"\n\nKontekst for denne session:\n{context}";
        }

        ChatMessage[] messages =
        [
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(fullTranscription)
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

        return new MemoResult(
            Content: contentBuilder.ToString(),
            ModelUsed: _gptDeployment,
            PromptTokens: promptTokens,
            CompletionTokens: completionTokens);
    }
}
