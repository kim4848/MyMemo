using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace MyMemo.Shared.Services;

public sealed class AudioConverterService(ILogger<AudioConverterService> logger) : IAudioConverterService
{
    public async Task<string> ConvertToWavAsync(Stream input)
    {
        var inputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.webm");
        var outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.wav");

        try
        {
            await using (var fileStream = File.Create(inputPath))
                await input.CopyToAsync(fileStream);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-i \"{inputPath}\" -ar 16000 -ac 1 -f wav \"{outputPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };

            process.Start();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                logger.LogError("ffmpeg failed with exit code {ExitCode}: {Stderr}", process.ExitCode, stderr);
                throw new InvalidOperationException($"ffmpeg conversion failed with exit code {process.ExitCode}");
            }

            return outputPath;
        }
        finally
        {
            if (File.Exists(inputPath))
                File.Delete(inputPath);
        }
    }
}
