using MediaManagement.Contracts.Uploads;

namespace MediaManagement.Contracts.Posts;

public sealed record SavePostRequest(
    string? Caption,
    IReadOnlyList<PostItemRequest> Items,
    IReadOnlyList<Guid> TagIds
);

public sealed record PostItemRequest(Guid MediaAssetId, string? AltText);

public sealed class GetPostsRequest
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed record PostDetails(
    Guid Id,
    string? Caption,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<PostItemDetails> Items,
    IReadOnlyList<PostTagDetails> Tags
);

public sealed record PostItemDetails(
    Guid Id,
    MediaAssetDetails? MediaAsset,
    int SortOrder,
    string? AltText
);

public sealed record PostTagDetails(Guid Id, string Name);

public sealed record PostPage(
    IReadOnlyList<PostDetails> Items,
    int PageNumber,
    int PageSize,
    int TotalCount
);
