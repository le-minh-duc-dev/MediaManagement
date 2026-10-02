using Asp.Versioning;
using MediaManagement.Contracts;
using MediaManagement.Contracts.Authentication;
using MediaManagement.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MediaManagement.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[EnableRateLimiting("api")]
[Route("api/v{version:apiVersion}/")]
public class AuthenticationController(IIdentityService identity) : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult> LoginAsync(
        LoginRequestDto request,
        CancellationToken cancellationToken
    )
    {
        var result = await identity.LoginAsync(request, cancellationToken);

        return result.ToNoContent(this);
    }

    [HttpPost("signup")]
    public async Task<ActionResult> SignupAsync(
        SignupRequestDto request,
        CancellationToken cancellationToken
    )
    {
        var result = await identity.SignupAsync(request, cancellationToken);

        return result.ToNoContent(this);
    }
}
