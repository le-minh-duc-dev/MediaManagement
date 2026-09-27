using System.Diagnostics.CodeAnalysis;

namespace MediaManagement.Interfaces.Services;

public sealed record StoredUpload(long SizeBytes, string ContentType);

public sealed record SignedUpload(string Url, IReadOnlyDictionary<string, string> Headers);

public interface IUploadStorage
{
    Task<SignedUpload> CreateUploadUrlAsync(
        string key,
        string contentType,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken
    );
    Task<StoredUpload?> GetMetadataAsync(string key, CancellationToken cancellationToken);
    Task DeleteAsync(string key, CancellationToken cancellationToken);

    [return: NotNull]
    Task<Dictionary<string, string>> GetPresignedDownloadUrlsAsync(
        ICollection<string> keys,
        CancellationToken cancellationToken
    );
}
