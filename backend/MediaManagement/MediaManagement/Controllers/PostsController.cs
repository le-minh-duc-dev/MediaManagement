using Asp.Versioning;
using MediaManagement.Contracts;
using MediaManagement.Contracts.Posts;
using MediaManagement.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MediaManagement.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[EnableRateLimiting("api")]
[Route("api/v{version:apiVersion}/posts")]
public sealed class PostsController(IPostService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<PostDetails>> CreateOwnAsync(
        SavePostRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await service.CreateOwnAsync(request, cancellationToken);
        return result.ToCreatedAtAction(
            this,
            nameof(GetOwnAsync),
            result.IsSuccess ? new { id = result.Value.Id } : null
        );
    }

    [HttpGet]
    public async Task<ActionResult<PostPage>> GetPageOwnAsync(
        [FromQuery] GetPostsRequest request,
        CancellationToken cancellationToken
    ) => (await service.GetPageOwnAsync(request, cancellationToken)).ToOk(this);

    [HttpGet("{id:guid}")]
    [ActionName(nameof(GetOwnAsync))]
    public async Task<ActionResult<PostDetails>> GetOwnAsync(
        Guid id,
        CancellationToken cancellationToken
    ) => (await service.GetOwnAsync(id, cancellationToken)).ToOk(this);

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PostDetails>> UpdateOwnAsync(
        Guid id,
        SavePostRequest request,
        CancellationToken cancellationToken
    ) => (await service.UpdateOwnAsync(id, request, cancellationToken)).ToOk(this);
}
