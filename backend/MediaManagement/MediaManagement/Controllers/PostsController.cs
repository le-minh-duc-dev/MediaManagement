using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace MediaManagement.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/posts")]
public class PostsController : ControllerBase { }
