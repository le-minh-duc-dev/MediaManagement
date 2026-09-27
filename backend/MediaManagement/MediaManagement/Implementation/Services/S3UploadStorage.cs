using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using MediaManagement.Interfaces.Services;
using MediaManagement.Models;
using Microsoft.Extensions.Options;

namespace MediaManagement.Implementation.Services;

public sealed class S3UploadStorage(
    IAmazonS3 s3,
    TimeProvider timeProvider,
    IOptions<UploadOptions> options
) : IUploadStorage
{
    public async Task<SignedUpload> CreateUploadUrlAsync(
        string key,
        string contentType,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        GetPreSignedUrlRequest request = new()
        {
            BucketName = options.Value.BucketName,
            Key = key,
            Verb = HttpVerb.PUT,
            ContentType = contentType,
            Expires = expiresAt.UtcDateTime,
            Protocol = Protocol.HTTPS,
        };
        // A reused URL cannot replace an object already verified by the API.
        request.Headers["If-None-Match"] = "*";
        var url = await s3.GetPreSignedURLAsync(request);
        return new SignedUpload(
            url,
            new Dictionary<string, string>
            {
                ["Content-Type"] = contentType,
                ["If-None-Match"] = "*",
            }
        );
    }

    public async Task<StoredUpload?> GetMetadataAsync(
        string key,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var response = await s3.GetObjectMetadataAsync(
                new GetObjectMetadataRequest { BucketName = options.Value.BucketName, Key = key },
                cancellationToken
            );
            return new StoredUpload(response.ContentLength, response.Headers.ContentType);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken) =>
        await s3.DeleteObjectAsync(
            new DeleteObjectRequest { BucketName = options.Value.BucketName, Key = key },
            cancellationToken
        );

    public async Task<Dictionary<string, string>> GetPresignedDownloadUrlsAsync(
        ICollection<string> keys,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(keys);

        var distinctKeys = keys.Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var tasks = distinctKeys
            .Select(key => GetPresignedDownloadUrlAsync(key, cancellationToken))
            .ToArray();

        var urls = await Task.WhenAll(tasks);

        return distinctKeys
            .Zip(urls)
            .ToDictionary(x => x.First, x => x.Second, StringComparer.Ordinal);
    }

    public async Task<string> GetPresignedDownloadUrlAsync(
        string key,
        CancellationToken cancellationToken
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        cancellationToken.ThrowIfCancellationRequested();

        var request = new GetPreSignedUrlRequest
        {
            BucketName = options.Value.BucketName,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = timeProvider
                .GetUtcNow()
                .AddMinutes(options.Value.DownloadUrlLifetimeMinutes)
                .UtcDateTime,
        };

        return await s3.GetPreSignedURLAsync(request);
    }
}
