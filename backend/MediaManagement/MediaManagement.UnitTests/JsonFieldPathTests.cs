using System.Text.Json;
using System.Text.Json.Serialization;
using MediaManagement.Api.Validation;

namespace MediaManagement.UnitTests;

public class JsonFieldPathTests
{
    [Theory]
    [InlineData("Items[0].DisplayName", "items[0].label")]
    [InlineData("$.items[0].label", "items[0].label")]
    [InlineData("", null)]
    [InlineData("$", null)]
    public void Paths_use_json_names_and_retain_collection_indexes(string input, string? expected)
    {
        Assert.Equal(expected, JsonFieldPath.Convert(input, typeof(Request), new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Fact]
    public void Root_collections_and_custom_naming_policies_are_supported()
    {
        Assert.Equal("[2].label", JsonFieldPath.Convert("[2].DisplayName", typeof(Item[]), new(JsonSerializerDefaults.Web)));
        Assert.Equal("[2].label", JsonFieldPath.Convert("$[2].label", typeof(List<Item>), new(JsonSerializerDefaults.Web)));
        Assert.Equal("total_count", JsonFieldPath.Convert("TotalCount", typeof(Request), new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }));
    }

    private sealed record Request(List<Item> Items, int TotalCount);
    private sealed record Item([property: JsonPropertyName("label")] string DisplayName);
}
