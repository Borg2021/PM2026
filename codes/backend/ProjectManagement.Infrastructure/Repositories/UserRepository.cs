using Microsoft.EntityFrameworkCore;
using ProjectManagement.Domain.Entities;
using ProjectManagement.Domain.Interfaces;
using ProjectManagement.Infrastructure.Data;

namespace ProjectManagement.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;

    public UserRepository(AppDbContext db) => _db = db;

    public async Task<User?> GetByUsernameAsync(string username)
    {
        return await _db.Users.FirstOrDefaultAsync(u => u.Username == username && u.Status == 1);
    }

    public async Task<User> AddAsync(User user)
    {
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    public async Task<bool> ExistsAsync(string username)
    {
        return await _db.Users.AnyAsync(u => u.Username == username);
    }

    public async Task<(List<User> Items, int Total)> GetPagedAsync(string? keyword, int pageIndex, int pageSize, string? departmentIds = null)
    {
        var query = _db.Users.AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLower();
            query = query.Where(u => u.Username.ToLower().Contains(kw) || u.RealName.ToLower().Contains(kw));
        }
        if (!string.IsNullOrWhiteSpace(departmentIds))
        {
            var ids = departmentIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => long.TryParse(s, out var id) ? id : (long?)null)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToHashSet();
            if (ids.Count > 0)
                query = query.Where(u => u.DepartmentId != null && ids.Contains(u.DepartmentId.Value));
        }
        var total = await query.CountAsync();
        var items = await query
            .Include(u => u.Department)
            .Include(u => u.UserFunctions).ThenInclude(uf => uf.Function)
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .OrderByDescending(u => u.CreatedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .AsSplitQuery()
            .ToListAsync();
        return (items, total);
    }

    public async Task<User?> GetByIdAsync(long id)
    {
        return await _db.Users
            .Include(u => u.Department)
            .Include(u => u.UserFunctions).ThenInclude(uf => uf.Function)
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task UpdateAsync(User user)
    {
        // 实体已在 GetByIdAsync 中被跟踪，直接保存即可，无需 _db.Update()
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(User user)
    {
        // 先删除关联的用户角色和用户职能
        var userRoles = _db.UserRoles.Where(ur => ur.UserId == user.Id);
        _db.UserRoles.RemoveRange(userRoles);
        var userFunctions = _db.UserFunctions.Where(uf => uf.UserId == user.Id);
        _db.UserFunctions.RemoveRange(userFunctions);
        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
    }
}
