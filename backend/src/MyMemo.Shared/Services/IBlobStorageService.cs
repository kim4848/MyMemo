namespace MyMemo.Shared.Services;

public interface IBlobStorageService
{
    Task<string> UploadAsync(string blobPath, Stream content, string contentType);
    Task<Stream> DownloadAsync(string blobPath);
    Uri GenerateSasUrl(string blobPath, TimeSpan expiry);
}
