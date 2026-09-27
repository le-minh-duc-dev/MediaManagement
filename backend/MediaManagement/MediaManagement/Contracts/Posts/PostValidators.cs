using FluentValidation;

namespace MediaManagement.Contracts.Posts;

public sealed class PostItemValidator : AbstractValidator<PostItemRequest>
{
    public PostItemValidator()
    {
        RuleFor(x => x.MediaAssetId).NotEmpty().WithErrorCode("post.media_asset_id.required");
        RuleFor(x => x.AltText).MaximumLength(1000).WithErrorCode("post.alt_text.too_long");
    }
}

public sealed class SavePostValidator : AbstractValidator<SavePostRequest>
{
    public SavePostValidator()
    {
        RuleFor(x => x.Caption).MaximumLength(5000).WithErrorCode("post.caption.too_long");
        RuleFor(x => x.Items).NotNull().WithErrorCode("post.items.required")
            .Must(x => x is null || x.Count is >= 1 and <= 100).WithErrorCode("post.items.invalid_count")
            .Must(x => x is null || x.Where(i => i is not null).Select(i => i.MediaAssetId).Distinct().Count() == x.Count)
            .WithErrorCode("post.items.duplicate");
        RuleForEach(x => x.Items).NotNull().WithErrorCode("post.item.required").SetValidator(new PostItemValidator());
        RuleFor(x => x.TagIds).NotNull().WithErrorCode("post.tag_ids.required")
            .Must(x => x is null || x.Count <= 30).WithErrorCode("post.tag_ids.too_many")
            .Must(x => x is null || x.Distinct().Count() == x.Count).WithErrorCode("post.tag_ids.duplicate");
        RuleForEach(x => x.TagIds).NotEmpty().WithErrorCode("post.tag_id.required");
    }
}

public sealed class GetPostsValidator : AbstractValidator<GetPostsRequest>
{
    public GetPostsValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThan(0).WithErrorCode("post.page_number.invalid");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithErrorCode("post.page_size.invalid");
        RuleFor(x => x).Must(x => ((long)x.PageNumber - 1) * x.PageSize <= int.MaxValue)
            .WithErrorCode("post.page_offset.too_large");
    }
}
