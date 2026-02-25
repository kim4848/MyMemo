using System.ClientModel;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace MyMemo.Shared.Services;

public sealed class MemoGeneratorService(IOptions<AzureOpenAIOptions> options) : IMemoGeneratorService
{
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
        - Vær koncis men præcis
        - Brug ikke mere end 1 side til referatet
        - Marker usikre punkter med [?]
        """;

    public async Task<MemoResult> GenerateAsync(string fullTranscription, string outputMode)
    {
        var credential = new ApiKeyCredential(options.Value.ApiKey);
        var client = new AzureOpenAIClient(new Uri(options.Value.Endpoint), credential);
        var chatClient = client.GetChatClient(options.Value.GptDeployment);

        var systemPrompt = outputMode == "summary" ? SummaryModePrompt : FullModePrompt;

        var result = await chatClient.CompleteChatAsync(
        [
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(fullTranscription)
        ]);

        var completion = result.Value;

        return new MemoResult(
            Content: completion.Content[0].Text,
            ModelUsed: options.Value.GptDeployment,
            PromptTokens: completion.Usage.InputTokenCount,
            CompletionTokens: completion.Usage.OutputTokenCount);
    }
}
