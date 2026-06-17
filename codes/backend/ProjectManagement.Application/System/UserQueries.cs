using MediatR;
using ProjectManagement.Application.Common;
using ProjectManagement.Domain.Interfaces;

namespace ProjectManagement.Application.System;

public record GetUserListQuery(
    string? Keyword,
    int PageIndex = 1,
    int PageSize = 10,
    string? DepartmentIds = null
) : IRequest<PagedResult<UserDto>>;

public class GetUserListHandler : IRequestHandler<GetUserListQuery, PagedResult<UserDto>>
{
    private readonly IUserRepository _userRepo;

    public GetUserListHandler(IUserRepository userRepo) => _userRepo = userRepo;

    public async Task<PagedResult<UserDto>> Handle(GetUserListQuery request, CancellationToken cancellationToken)
    {
        var (items, total) = await _userRepo.GetPagedAsync(request.Keyword, request.PageIndex, request.PageSize, request.DepartmentIds);

        return new PagedResult<UserDto>
        {
            Total = total,
            Items = items.Select(u => new UserDto
            {
                Id = u.Id,
                Username = u.Username,
                RealName = u.RealName,
                Role = u.Role,
                Status = u.Status,
                CreatedAt = u.CreatedAt,
                DepartmentId = u.DepartmentId,
                DepartmentName = u.Department?.Name,
                FunctionIds = u.UserFunctions.Select(uf => uf.FunctionId).ToList(),
                FunctionNames = string.Join("、", u.UserFunctions.Select(uf => uf.Function.Name)),
                RbacRoleNames = u.UserRoles.Select(ur => ur.Role.Name).ToList()
            }).ToList()
        };
    }
}
