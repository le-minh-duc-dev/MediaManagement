namespace MediaManagement.Interfaces.Services;

public interface ICurrentUser
{
    Guid? UserId { get; }

    /// <summary>
    /// Gets the required user ID from the current user context. If the user ID is not found, an InvalidOperationException is thrown.
    /// </summary>
    /// <returns>The required user ID.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the user ID is not found.</exception>
    Guid GetRequiredUserId();
    bool IsAuthenticated { get; }
}
