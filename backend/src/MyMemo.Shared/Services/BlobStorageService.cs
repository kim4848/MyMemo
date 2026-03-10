using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.Extensions.Options;

namespace MyMemo.Shared.Services;

public sealed class BlobStorageService(IOptions<AzureBlobOptions> options) : IBlobStorageService
{
    private readonly BlobContainerClient _container = new(options.Value.ConnectionString, options.Value.ContainerName);
    private bool _containerEnsured;

    private async Task EnsureContainerAsync()
    {
        if (_containerEnsured) return;
        await _container.CreateIfNotExistsAsync();
        _containerEnsured = true;
    }

    public async Task<string> UploadAsync(string blobPath, Stream content, string contentType)
    {
        await EnsureContainerAsync();
        var blob = _container.GetBlobClient(blobPath);
        await blob.UploadAsync(content, overwrite: true);
        return blobPath;
    }

    public async Task<Stream> DownloadAsync(string blobPath)
    {
        var blob = _container.GetBlobClient(blobPath);
        var response = await blob.DownloadStreamingAsync();
        return response.Value.Content;
    }

    public Uri GenerateSasUrl(string blobPath, TimeSpan expiry)
    {
        var blob = _container.GetBlobClient(blobPath);
        var sasUri = blob.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.Add(expiry));
        return sasUri;
    }
}
