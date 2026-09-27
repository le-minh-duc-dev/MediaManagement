using FluentValidation;
using MediaManagement.Contracts.Posts;
using MediaManagement.Contracts.Uploads;
using MediaManagement.Entities;
using MediaManagement.Interfaces.Repositories;
using MediaManagement.Interfaces.Services;
using MediaManagement.Models.Results;

namespace MediaManagement.Implementation.Services;

public sealed class PostService(
    ICurrentUser CurrentUser,
    IUploadStorage uploadStorage,
    IPostRepository repository,
    TimeProvider clock
) : ServiceBase(CurrentUser), IPostService
{
    public Task<Result<PostDetails>> CreateOwnAsync(
        SavePostRequest request,
        CancellationToken cancellationToken
    ) => SaveOwnAsync(null, request, cancellationToken);

    public Task<Result<PostDetails>> UpdateOwnAsync(
        Guid id,
        SavePostRequest request,
        CancellationToken cancellationToken
    ) => SaveOwnAsync(id, request, cancellationToken);

    public async Task<Result<PostDetails>> GetOwnAsync(Guid id, CancellationToken cancellationToken)
    {
        var ownerId = CurrentUser.GetRequiredUserId();
        var post = await repository.GetByOwnerAsync(ownerId, id, true, cancellationToken);
        return post is null
            ? Missing()
            : Result<PostDetails>.Success(await DetailsAsync(post, cancellationToken));
    }

    public async Task<Result<PostPage>> GetPageOwnAsync(
        GetPostsRequest request,
        CancellationToken cancellationToken
    )
    {
        if (
            request.PageNumber <= 0
            || request.PageSize is < 1 or > 100
            || (long)(request.PageNumber - 1) * request.PageSize > int.MaxValue
        )
        {
            return Result<PostPage>.Failure(
                ErrorType.Validation,
                new Error("post.pagination.invalid")
            );
        }

        var ownerId = CurrentUser.GetRequiredUserId();

        var (Items, TotalCount) = await repository.GetPageAsync(
            ownerId,
            (request.PageNumber - 1) * request.PageSize,
            request.PageSize,
            cancellationToken
        );

        return Result<PostPage>.Success(
            new(
                [
                    .. await Task.WhenAll(
                        Items.Select(item => DetailsAsync(item, cancellationToken))
                    ),
                ],
                request.PageNumber,
                request.PageSize,
                TotalCount
            )
        );
    }

    private async Task<Result<PostDetails>> SaveOwnAsync(
        Guid? id,
        SavePostRequest request,
        CancellationToken cancellationToken
    )
    {
        var ownerId = CurrentUser.GetRequiredUserId();

        if (request.Caption?.Length > 5000)
        {
            return Result<PostDetails>.Failure(
                ErrorType.Validation,
                new Error("post.caption.too_long", nameof(request.Caption))
            );
        }

        var post = id.HasValue
            ? await repository.GetByOwnerAsync(ownerId, id.Value, false, cancellationToken)
            : null;

        if (id.HasValue && post is null)
        {
            return Missing();
        }

        Guid[] assetIds = [.. request.Items.Select(x => x.MediaAssetId)];
        if (
            await repository.CountAvailableAssetsAsync(ownerId, assetIds, cancellationToken)
            != assetIds.Length
        )
        {
            return Result<PostDetails>.Failure(
                ErrorType.Validation,
                new Error(BusinessErrorCodes.Post.MediaAssetIdsMismatch, nameof(request.Items))
            );
        }

        var tags = await repository.GetTagsAsync(request.TagIds, cancellationToken);
        if (tags.Count != request.TagIds.Count)
        {
            return Result<PostDetails>.Failure(
                ErrorType.Validation,
                new Error(BusinessErrorCodes.Post.TagsMismatch, nameof(request.TagIds))
            );
        }

        if (post is null)
        {
            post = new Post
            {
                Id = CreateNewGuid(),
                OwnerId = ownerId,
                CreatedAt = clock.GetUtcNow(),
            };

            repository.Add(post);
        }
        else
        {
            post.UpdatedAt = clock.GetUtcNow();
        }
        post.Caption = request.Caption;

        var existing = post.Items.ToDictionary(x => x.MediaAssetId);
        foreach (var item in post.Items.Where(x => !assetIds.Contains(x.MediaAssetId)).ToArray())
        {
            post.Items.Remove(item);
        }

        for (var index = 0; index < request.Items.Count; index++)
        {
            var input = request.Items[index];

            if (!existing.TryGetValue(input.MediaAssetId, out var item))
            {
                item = new PostItem
                {
                    Id = CreateNewGuid(),
                    PostId = post.Id,
                    MediaAssetId = input.MediaAssetId,
                };

                post.Items.Add(item);
                repository.AddItem(item);
            }

            item.SortOrder = index;
            item.AltText = input.AltText;
        }

        foreach (var tag in post.Tags.Where(x => !request.TagIds.Contains(x.Id)).ToArray())
        {
            post.Tags.Remove(tag);
        }

        foreach (var tag in tags.Where(x => post.Tags.All(t => t.Id != x.Id)))
        {
            post.Tags.Add(tag);
        }

        await repository.SaveChangesAsync(cancellationToken);

        Post? postWithDetailsSource = await repository.GetByOwnerAsync(
            ownerId,
            post.Id,
            true,
            cancellationToken
        );
        if (postWithDetailsSource is null)
        {
            return Missing();
        }

        var postWithDetails = await DetailsAsync(postWithDetailsSource, cancellationToken);

        return Result<PostDetails>.Success(postWithDetails);
    }

    private async Task<PostDetails> DetailsAsync(Post post, CancellationToken cancellationToken)
    {
        var downloadUrlDictionary = await uploadStorage.GetPresignedDownloadUrlsAsync(
            [
                .. post
                    .Items.Where(item => item.MediaAsset is not null)
                    .Select(item => item.MediaAsset!.ObjectKey),
            ],
            cancellationToken
        );
        return new(
            post.Id,
            post.Caption,
            post.CreatedAt,
            post.UpdatedAt,
            [
                .. post
                    .Items.OrderBy(x => x.SortOrder)
                    .ThenBy(x => x.Id)
                    .Select(x => new PostItemDetails(
                        x.Id,
                        Details(x.MediaAsset, downloadUrlDictionary[x.MediaAsset!.ObjectKey]),
                        x.SortOrder,
                        x.AltText
                    )),
            ],
            [
                .. post
                    .Tags.OrderBy(x => x.Name)
                    .ThenBy(x => x.Id)
                    .Select(x => new PostTagDetails(x.Id, x.Name)),
            ]
        );
    }

    private static MediaAssetDetails? Details(MediaAsset? asset, string downloadUrl) =>
        asset is null ? null : new(asset.Id, asset.FileName, asset.ContentType, downloadUrl);

    private static Result<PostDetails> Missing() =>
        Result<PostDetails>.Failure(
            ErrorType.NotFound,
            new Error(BusinessErrorCodes.Post.NotFound)
        );
}
