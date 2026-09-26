using MediaManagement.Entities;
using Serilog.Core;
using Serilog.Events;

namespace MediaManagement;

public sealed class CustomPolicy : IDestructuringPolicy
{
    public bool TryDestructure(
        object value,
        ILogEventPropertyValueFactory propertyValueFactory,
        out LogEventPropertyValue result
    )
    {
        // Explicit projections prevent new entity fields and navigation properties
        // from being included in logs automatically.
        object? summary = value switch
        {
            MediaAsset asset => new
            {
                asset.Id,
                asset.UploadSessionId,
                asset.ContentType,
                asset.SizeBytes,
                asset.CreatedAt,
            },
            UploadSession session => new
            {
                session.Id,
                session.Status,
                session.ExpectedSizeBytes,
                session.CreatedAt,
                session.ExpiresAt,
                session.CompletedAt,
            },
            Post post => new
            {
                post.Id,
                post.CreatedAt,
                post.UpdatedAt,
            },
            PostItem item => new
            {
                item.Id,
                item.PostId,
                item.MediaAssetId,
                item.SortOrder,
            },
            _ => null,
        };

        if (summary is null)
        {
            result = null!;
            return false;
        }

        // Use Serilog's factory so configured destructuring limits still apply.
        result = propertyValueFactory.CreatePropertyValue(summary, destructureObjects: true);
        return true;
    }
}
