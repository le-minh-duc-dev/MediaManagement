using FluentValidation;
using MediaManagement.Contracts.Uploads;
using MediaManagement.Entities;
using MediaManagement.Interfaces.Repositories;
using MediaManagement.Interfaces.Services;
using MediaManagement.Models;
using MediaManagement.Models.Results;
using Microsoft.Extensions.Options;

namespace MediaManagement.Implementation.Services;

public sealed class UploadSessionService(
    ICurrentUser CurrentUser,
    IUploadSessionRepository repository,
    IUploadStorage storage,
    IOptions<UploadOptions> options,
    TimeProvider clock
) : ServiceBase(CurrentUser), IUploadSessionService
{
    public async Task<Result<CreatedUploadSession>> CreateOwnAsync(
        CreateUploadSessionRequest request,
        CancellationToken cancellationToken
    )
    {
        var createdBy = CurrentUser.GetRequiredUserId();

        var now = clock.GetUtcNow();
        UploadSession session = new()
        {
            Id = CreateNewGuid(),
            CreatedBy = createdBy,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(options.Value.SessionLifetimeMinutes),
            NextCleanupAt = now.AddHours(options.Value.CleanupAfterHours),
            Revision = CreateNewGuid(),
            Status = UploadSessionStatus.Pending,
            ExpectedSizeBytes = request.Items.Sum(x => x.SizeBytes),
        };
        foreach (var item in request.Items)
        {
            var id = CreateNewGuid();
            session.MediaAssets.Add(
                new MediaAsset
                {
                    Id = id,
                    CreatedBy = createdBy,
                    UploadSessionId = session.Id,
                    ObjectKey = CreateObjectKey(session.Id, id),
                    FileName = item.FileName,
                    ContentType = item.ContentType.ToMimeType(),
                    SizeBytes = item.SizeBytes,
                    CreatedAt = now,
                }
            );
        }

        repository.Add(session);
        await repository.SaveChangesAsync(cancellationToken);

        List<UploadTarget> targets = [];
        var urlExpiresAt = now.AddMinutes(options.Value.UrlLifetimeMinutes);
        foreach (var asset in session.MediaAssets)
        {
            var signed = await storage.CreateUploadUrlAsync(
                asset.ObjectKey,
                asset.ContentType,
                urlExpiresAt,
                cancellationToken
            );
            targets.Add(
                new UploadTarget(
                    asset.Id,
                    asset.FileName,
                    asset.SizeBytes,
                    asset.ContentType.ToMediaContentType(),
                    signed.Url,
                    "PUT",
                    signed.Headers,
                    urlExpiresAt
                )
            );
        }

        return Result<CreatedUploadSession>.Success(new(session.Id, session.ExpiresAt, targets));
    }

    public async Task<Result<UploadSessionDetails>> GetOwnAsync(
        Guid id,
        CancellationToken cancellationToken
    )
    {
        var createdBy = CurrentUser.GetRequiredUserId();
        var session = await GetSessionForOwnerAsync(createdBy, id, cancellationToken);
        return session is null
            ? Result<UploadSessionDetails>.Failure(
                ErrorType.NotFound,
                new Error(BusinessErrorCodes.NotFound)
            )
            : Result<UploadSessionDetails>.Success(Details(session));
    }

    public async Task<Result<UploadSessionDetails>> CompleteOwnAsync(
        Guid id,
        CompleteUploadSessionRequest request,
        CancellationToken cancellationToken
    )
    {
        var createdBy = CurrentUser.GetRequiredUserId();

        var session = await GetSessionForOwnerAsync(createdBy, id, cancellationToken);

        if (session is null)
        {
            return Result<UploadSessionDetails>.Failure(
                ErrorType.NotFound,
                new Error(BusinessErrorCodes.UploadSession.NotFound)
            );
        }

        HashSet<Guid> ids = [.. request.UploadedMediaAssetIds];

        if (session.Status is UploadSessionStatus.Completed or UploadSessionStatus.Failed)
        {
            return ids.SetEquals(
                session.MediaAssets.Where(x => x.UploadedAt != null).Select(x => x.Id)
            )
                ? Result<UploadSessionDetails>.Success(Details(session))
                : Conflict(BusinessErrorCodes.UploadSession.UploadedMediaAssetIdsMismatch);
        }

        if (session.Status != UploadSessionStatus.Pending || session.ExpiresAt <= clock.GetUtcNow())
        {
            return Conflict(BusinessErrorCodes.UploadSession.StatusExpired);
        }

        if (ids.Except(session.MediaAssets.Select(x => x.Id)).Any())
        {
            return Result<UploadSessionDetails>.Failure(
                ErrorType.Validation,
                new Error(
                    BusinessErrorCodes.UploadSession.AssetNotInSession,
                    nameof(request.UploadedMediaAssetIds)
                )
            );
        }

        foreach (var asset in session.MediaAssets.Where(x => ids.Contains(x.Id)))
        {
            var metadata = await storage.GetMetadataAsync(asset.ObjectKey, cancellationToken);
            if (metadata is null)
            {
                return Conflict(BusinessErrorCodes.NotFound);
            }

            if (
                metadata.SizeBytes != asset.SizeBytes
                || !string.Equals(
                    metadata.ContentType,
                    asset.ContentType,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return Conflict(BusinessErrorCodes.UploadSession.ObjectMetadataMismatch);
            }
        }
        var now = clock.GetUtcNow();
        if (session.ExpiresAt <= now)
        {
            return Conflict(BusinessErrorCodes.UploadSession.StatusExpired);
        }

        foreach (var asset in session.MediaAssets.Where(x => ids.Contains(x.Id)))
        {
            asset.UploadedAt = now;
        }

        session.Status = ids.Count > 0 ? UploadSessionStatus.Completed : UploadSessionStatus.Failed;
        session.CompletedAt = now;
        session.Revision = CreateNewGuid();

        if (!await repository.TrySaveChangesAsync(cancellationToken))
        {
            var current = await GetSessionForOwnerAsync(createdBy, id, cancellationToken);
            return
                current is not null
                && current.Status is UploadSessionStatus.Completed or UploadSessionStatus.Failed
                && ids.SetEquals(
                    current.MediaAssets.Where(x => x.UploadedAt != null).Select(x => x.Id)
                )
                ? Result<UploadSessionDetails>.Success(Details(current))
                : Conflict(BusinessErrorCodes.Invalid);
        }

        return Result<UploadSessionDetails>.Success(Details(session));
    }

    private UploadSessionDetails Details(UploadSession session) =>
        new(
            session.Id,
            session.Status == UploadSessionStatus.Pending && session.ExpiresAt <= clock.GetUtcNow()
                ? nameof(UploadSessionStatus.Expired)
                : session.Status.ToString(),
            session.ExpiresAt,
            session.CompletedAt,
            [
                .. session
                    .MediaAssets.OrderBy(x => x.Id)
                    .Select(x => new UploadAssetDetails(
                        x.Id,
                        x.FileName,
                        x.SizeBytes,
                        x.ContentType.ToMediaContentType(),
                        x.UploadedAt is not null
                    )),
            ]
        );

    private Task<UploadSession?> GetSessionForOwnerAsync(
        Guid createdBy,
        Guid sessionId,
        CancellationToken cancellationToken
    )
    {
        var specification = new SpecifcationBase<UploadSession>(x =>
            x.CreatedBy == createdBy
        ).Include(x => x.MediaAssets);
        return repository.GetByIdAsync(
            sessionId,
            specification,
            asNoTracking: false,
            cancellationToken: cancellationToken
        );
    }

    private static Result<UploadSessionDetails> Conflict(string code) =>
        Result<UploadSessionDetails>.Failure(ErrorType.Conflict, new Error(code));

    private string CreateObjectKey(Guid sessionId, Guid assetId) =>
        $"{CurrentUser.GetRequiredUserId():N}/{sessionId:N}/{assetId:N}";
}
