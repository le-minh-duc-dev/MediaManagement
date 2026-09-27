using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using MediaManagement.Contracts.Posts;
using MediaManagement.Database;
using MediaManagement.Entities;
using MediaManagement.Interfaces.Services;
using MediaManagement.Models.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace MediaManagement.IntegrationTests;

public sealed class PostTests : IAsyncLifetime
{
    private readonly UploadFactory factory = new();
    private readonly Guid owner = Guid.NewGuid();
    private HttpClient client = null!;
    private const string Route = "/api/v1/posts";

    public async Task InitializeAsync()
    {
        client = factory.CreateClient(new()
        {
            BaseAddress = new Uri("https://localhost")
        });
        SetOwner(owner);
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<MediaManagementContext>().Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        client.Dispose();
        await factory.DisposeAsync();
    }

    [Fact]
    public async Task Create_and_update_persist_order_membership_tags_and_timestamps()
    {
        Guid first = await AssetAsync(owner);
        Guid second = await AssetAsync(owner);
        Guid third = await AssetAsync(owner);
        Guid tagId = Guid.NewGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MediaManagementContext>();
            db.Set<Tag>().Add(new Tag { Id = tagId, Name = "travel" });
            await db.SaveChangesAsync();
        }
        var response = await client.PostAsJsonAsync(Route, new SavePostRequest("original", [new(first, "first"), new(second, null)], [tagId]));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<PostDetails>())!;
        Assert.EndsWith($"{Route}/{created.Id}", response.Headers.Location!.ToString());
        Assert.Null(created.UpdatedAt);
        Assert.Equal(tagId, Assert.Single(created.Tags).Id);
        Assert.Equal(new[] { first, second }, created.Items.Select(x => x.MediaAsset!.MediaAssetId));
        factory.Clock.Advance(TimeSpan.FromMinutes(1));
        var updatedResponse = await client.PutAsJsonAsync($"{Route}/{created.Id}", new SavePostRequest(null, [new(second, "changed"), new(third, null)], []));
        Assert.Equal(HttpStatusCode.OK, updatedResponse.StatusCode);
        var updated = (await client.GetFromJsonAsync<PostDetails>($"{Route}/{created.Id}"))!;
        Assert.Null(updated.Caption);
        Assert.Empty(updated.Tags);
        Assert.Equal(created.CreatedAt, updated.CreatedAt);
        Assert.Equal(factory.Clock.GetUtcNow(), updated.UpdatedAt);
        Assert.Equal(new[] { second, third }, updated.Items.Select(x => x.MediaAsset!.MediaAssetId));
        Assert.Equal(new[] { 0, 1 }, updated.Items.Select(x => x.SortOrder));
        Assert.Equal(created.Items[1].Id, updated.Items[0].Id);
        Assert.Equal("changed", updated.Items[0].AltText);
        await using var finalScope = factory.Services.CreateAsyncScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<MediaManagementContext>();
        Assert.Equal(2, await finalDb.Set<PostItem>().CountAsync());
        Assert.Equal(3, await finalDb.MediaAssets.CountAsync());
        Assert.Equal(1, await finalDb.Set<Tag>().CountAsync());
    }

    [Fact]
    public async Task Pagination_is_bounded_newest_first_stable_for_ties_and_owner_scoped()
    {
        Guid asset = await AssetAsync(owner);
        var old = await CreateAsync(asset);
        factory.Clock.Advance(TimeSpan.FromMinutes(1));
        var second = await CreateAsync(asset);
        var third = await CreateAsync(asset);
        Guid other = Guid.NewGuid();
        SetOwner(other);
        await CreateAsync(await AssetAsync(other));
        SetOwner(owner);
        var firstPage = (await client.GetFromJsonAsync<PostPage>($"{Route}?pageSize=2"))!;
        var secondPage = (await client.GetFromJsonAsync<PostPage>($"{Route}?pageNumber=2&pageSize=2"))!;
        Assert.Equal(3, firstPage.TotalCount);
        Assert.Equal(2, firstPage.Items.Count);
        Assert.Equal(new[] { second.Id, third.Id }.OrderByDescending(x => x), firstPage.Items.Select(x => x.Id));
        Assert.Equal(old.Id, Assert.Single(secondPage.Items).Id);
        Assert.Empty((await client.GetFromJsonAsync<PostPage>($"{Route}?pageNumber=4&pageSize=2"))!.Items);
    }

    [Theory]
    [InlineData("pageNumber=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("pageNumber=2147483647&pageSize=100")]
    public async Task Invalid_pagination_returns_bad_request(string query) =>
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"{Route}?{query}")).StatusCode);

    [Fact]
    public async Task Other_owners_cannot_read_or_update_posts_and_anonymous_requests_are_rejected()
    {
        Guid asset = await AssetAsync(owner);
        var created = await CreateAsync(asset);
        SetOwner(Guid.NewGuid());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Route}/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"{Route}/{created.Id}", new SavePostRequest("bad", [new(asset, null)], []))).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<PostPage>(Route))!.Items);
        client.DefaultRequestHeaders.Remove("X-Test-Owner");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Route)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(Route, new SavePostRequest(null, [new(asset, null)], []))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync($"{Route}/{created.Id}", new SavePostRequest(null, [new(asset, null)], []))).StatusCode);
    }

    [Fact]
    public async Task Unavailable_media_and_unknown_tags_do_not_create_or_modify_posts()
    {
        Guid valid = await AssetAsync(owner);
        var post = await CreateAsync(valid);
        Guid[] invalid = [Guid.NewGuid(), await AssetAsync(Guid.NewGuid()), await AssetAsync(owner, false)];
        foreach (Guid asset in invalid)
        {
            var request = new SavePostRequest("invalid", [new(asset, null)], []);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Route, request)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"{Route}/{post.Id}", request)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"{Route}/{post.Id}", new SavePostRequest("invalid", [new(valid, null)], [Guid.NewGuid()]))).StatusCode);
        var unchanged = (await client.GetFromJsonAsync<PostDetails>($"{Route}/{post.Id}"))!;
        Assert.Equal("caption", unchanged.Caption);
        Assert.Null(unchanged.UpdatedAt);
        Assert.Equal(valid, Assert.Single(unchanged.Items).MediaAsset!.MediaAssetId);
        Assert.Equal(1, (await client.GetFromJsonAsync<PostPage>(Route))!.TotalCount);
    }

    [Theory]
    [InlineData("{\"items\":null,\"tagIds\":[]}")]
    [InlineData("{\"items\":[],\"tagIds\":[]}")]
    [InlineData("{\"items\":[null],\"tagIds\":[]}")]
    [InlineData("{\"items\":[{\"mediaAssetId\":\"00000000-0000-0000-0000-000000000000\"}],\"tagIds\":null}")]
    public async Task Invalid_payloads_return_bad_request(string json)
    {
        using var body = new StringContent(json, Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync(Route, body)).StatusCode);
    }

    [Fact]
    public async Task Service_enforces_validation_without_MVC()
    {
        Guid asset = await AssetAsync(owner);
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext =
            new DefaultHttpContext
            {
                User = new ClaimsPrincipal(
                    new ClaimsIdentity([new Claim("sub", owner.ToString())], "ServiceTest")
                ),
            };
        var service = scope.ServiceProvider.GetRequiredService<IPostService>();
        var duplicate = await service.CreateOwnAsync(new(null, [new(asset, null), new(asset, null)], []), default);
        Assert.Equal(ErrorType.Validation, duplicate.ErrorType);
        Assert.False((await service.GetPageOwnAsync(new()
        {
            PageSize = 101
        }, default)).IsSuccess);
        Assert.False((await service.CreateOwnAsync(new(null, [new(Guid.Empty, null)], []), default)).IsSuccess);
        Assert.False((await service.CreateOwnAsync(new(new string('x', 5001), [new(asset, null)], []), default)).IsSuccess);
    }

    private async Task<PostDetails> CreateAsync(Guid asset)
    {
        var response = await client.PostAsJsonAsync(Route, new SavePostRequest("caption", [new(asset, null)], []));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PostDetails>())!;
    }

    private async Task<Guid> AssetAsync(Guid assetOwner, bool uploaded = true)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaManagementContext>();
        Guid id = Guid.NewGuid();
        db.UploadSessions.Add(new UploadSession
        {
            Id = Guid.NewGuid(),
            OwnerId = assetOwner,
            Revision = Guid.NewGuid(),
            Status = uploaded ? UploadSessionStatus.Completed : UploadSessionStatus.Pending,
            CreatedAt = factory.Clock.GetUtcNow(),
            ExpiresAt = factory.Clock.GetUtcNow().AddHours(1),
            MediaAssets = [new MediaAsset { Id = id, OwnerId = assetOwner, ObjectKey = id.ToString(),
                FileName = "photo.jpg", ContentType = "image/jpeg", SizeBytes = 10,
                CreatedAt = factory.Clock.GetUtcNow(), UploadedAt = uploaded ? factory.Clock.GetUtcNow() : null }]
        });
        await db.SaveChangesAsync();
        return id;
    }

    private void SetOwner(Guid value)
    {
        client.DefaultRequestHeaders.Remove("X-Test-Owner");
        client.DefaultRequestHeaders.Add("X-Test-Owner", value.ToString());
    }
}
