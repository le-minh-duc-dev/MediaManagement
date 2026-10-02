using MediaManagement.Database;
using MediaManagement.Entities;
using MediaManagement.Interfaces.Repositories;

namespace MediaManagement.Implementation.Repositories;

public class UserProfileRepository(MediaManagementContext context)
    : GenericRepository<UserProfile>(context),
        IUserProfileRepository { }
