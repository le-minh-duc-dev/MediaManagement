using MediaManagement.Contracts.Posts;
using MediaManagement.Models.Results;

namespace MediaManagement.Interfaces.Services;

public interface IPostService
{
    Task<Result<PostDetails>> CreateOwnAsync(
        SavePostRequest request,
        CancellationToken cancellationToken
    );
    Task<Result<PostDetails>> GetOwnAsync(Guid id, CancellationToken cancellationToken);
    Task<Result<PostPage>> GetPageOwnAsync(
        GetPostsRequest request,
        CancellationToken cancellationToken
    );
    Task<Result<PostDetails>> UpdateOwnAsync(
        Guid id,
        SavePostRequest request,
        CancellationToken cancellationToken
    );
}
