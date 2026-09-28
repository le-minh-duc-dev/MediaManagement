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
    public async Task<IActionResult> CreateOwnAsync(
        SavePostRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await service.CreateOwnAsync(request, cancellationToken);
        return result.IsSuccess
            ? Created($"{Request.Path}/{result.Value.Id}", result.Value)
            : result.ToOk(this);
    }

    [HttpGet]
    public async Task<IActionResult> GetPageOwnAsync(
        [FromQuery] GetPostsRequest request,
        CancellationToken cancellationToken
    ) => (await service.GetPageOwnAsync(request, cancellationToken)).ToOk(this);

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetOwnAsync(Guid id, CancellationToken cancellationToken) =>
        (await service.GetOwnAsync(id, cancellationToken)).ToOk(this);

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateOwnAsync(
        Guid id,
        SavePostRequest request,
        CancellationToken cancellationToken
    ) => (await service.UpdateOwnAsync(id, request, cancellationToken)).ToOk(this);
}
