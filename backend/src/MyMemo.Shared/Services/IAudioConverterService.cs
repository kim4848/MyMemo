namespace MyMemo.Shared.Services;

public interface IAudioConverterService
{
    Task<string> ConvertToWavAsync(Stream input);
}
