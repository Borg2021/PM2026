using ProjectManagement.Domain.Entities;

namespace ProjectManagement.Domain.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByUsernameAsync(string username);
    Task<User> AddAsync(User user);
    Task<bool> ExistsAsync(string username);
    Task<(List<User> Items, int Total)> GetPagedAsync(string? keyword, int pageIndex, int pageSize, string? departmentIds = null);
    Task<User?> GetByIdAsync(long id);
    Task UpdateAsync(User user);
    Task DeleteAsync(User user);
}
