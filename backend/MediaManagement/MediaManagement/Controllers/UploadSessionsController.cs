using Asp.Versioning;
using MediaManagement.Api;
using MediaManagement.Contracts.Uploads;
using MediaManagement.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MediaManagement.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[EnableRateLimiting("api")]
[Route("api/v{version:apiVersion}/upload-sessions")]
public sealed class UploadSessionsController(IUploadSessionService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateOwnAsync(
        CreateUploadSessionRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await service.CreateOwnAsync(request, cancellationToken);
        return result.IsSuccess
            ? Created($"{Request.Path}/{result.Value.Id}", result.Value)
            : result.ToOk(this);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetOwnAsync(Guid id, CancellationToken cancellationToken)
    {
        return (await service.GetOwnAsync(id, cancellationToken)).ToOk(this);
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> CompleteOwnAsync(
        Guid id,
        CompleteUploadSessionRequest request,
        CancellationToken cancellationToken
    )
    {
        return (await service.CompleteOwnAsync(id, request, cancellationToken)).ToOk(this);
    }
}
