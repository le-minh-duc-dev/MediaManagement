namespace MediaManagement.Contracts.Uploads;

public sealed record CreateUploadSessionRequest(List<UploadItemRequest> Items);

public sealed record UploadItemRequest(string FileName, long SizeBytes, MediaContentType ContentType);

public sealed record CompleteUploadSessionRequest(List<Guid> UploadedMediaAssetIds);

public sealed record UploadTarget(
    Guid MediaAssetId,
    string FileName,
    long SizeBytes,
    MediaContentType ContentType,
    string UploadUrl,
    string Method,
    IReadOnlyDictionary<string, string> Headers,
    DateTimeOffset UrlExpiresAt
);

public sealed record CreatedUploadSession(
    Guid Id,
    DateTimeOffset ExpiresAt,
    IReadOnlyList<UploadTarget> Items
);

public sealed record UploadSessionDetails(
    Guid Id,
    string Status,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<UploadAssetDetails> Items
);

public sealed record UploadAssetDetails(
    Guid MediaAssetId,
    string FileName,
    long SizeBytes,
    MediaContentType ContentType,
    bool Uploaded
);

public sealed record MediaAssetDetails(
    Guid MediaAssetId,
    string FileName,
    MediaContentType ContentType,
    string Url
);
