using MediaManagement.Contracts.Uploads;
using MediaManagement.Models.Results;

namespace MediaManagement.Interfaces.Services;

public interface IUploadSessionService
{
    Task<Result<CreatedUploadSession>> CreateOwnAsync(
        CreateUploadSessionRequest request,
        CancellationToken cancellationToken
    );

    Task<Result<UploadSessionDetails>> GetOwnAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<UploadSessionDetails>> CompleteOwnAsync(
        Guid id,
        CompleteUploadSessionRequest request,
        CancellationToken cancellationToken
    );
}
