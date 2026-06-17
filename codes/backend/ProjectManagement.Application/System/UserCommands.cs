using MediatR;
using ProjectManagement.Domain.Entities;
using ProjectManagement.Domain.Interfaces;

namespace ProjectManagement.Application.System;

/* ---- Create ---- */
public record CreateUserCommand(
    string Username,
    string Password,
    string RealName,
    string Role,
    long? DepartmentId,
    List<long> FunctionIds
) : IRequest<long>;

public class CreateUserHandler : IRequestHandler<CreateUserCommand, long>
{
    private readonly IUserRepository _userRepo;

    public CreateUserHandler(IUserRepository userRepo) => _userRepo = userRepo;

    public async Task<long> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        if (await _userRepo.ExistsAsync(request.Username))
            throw new InvalidOperationException("用户名已存在");

        var user = new User
        {
            Username = request.Username,
            Password = BCrypt.Net.BCrypt.HashPassword(request.Password),
            RealName = request.RealName,
            Role = request.Role,
            Status = 1,
            CreatedAt = DateTime.UtcNow,
            DepartmentId = request.DepartmentId,
            UserFunctions = request.FunctionIds.Select(fid => new UserFunction { FunctionId = fid }).ToList()
        };

        var result = await _userRepo.AddAsync(user);
        return result.Id;
    }
}

/* ---- Update ---- */
public record UpdateUserCommand(
    long Id,
    string RealName,
    string Role,
    int Status,
    long? DepartmentId,
    List<long> FunctionIds
) : IRequest;

public class UpdateUserHandler : IRequestHandler<UpdateUserCommand>
{
    private readonly IUserRepository _userRepo;

    public UpdateUserHandler(IUserRepository userRepo) => _userRepo = userRepo;

    public async Task Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepo.GetByIdAsync(request.Id);
        if (user == null) throw new InvalidOperationException("用户不存在");

        user.RealName = request.RealName;
        user.Role = request.Role;
        user.Status = request.Status;
        user.DepartmentId = request.DepartmentId;

        // 更新用户职能
        user.UserFunctions.Clear();
        foreach (var fid in request.FunctionIds)
            user.UserFunctions.Add(new UserFunction { UserId = user.Id, FunctionId = fid });

        await _userRepo.UpdateAsync(user);
    }
}

/* ---- Delete (soft) ---- */
public record DeleteUserCommand(long Id) : IRequest;

public class DeleteUserHandler : IRequestHandler<DeleteUserCommand>
{
    private readonly IUserRepository _userRepo;

    public DeleteUserHandler(IUserRepository userRepo) => _userRepo = userRepo;

    public async Task Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepo.GetByIdAsync(request.Id);
        if (user == null) throw new InvalidOperationException("用户不存在");

        await _userRepo.DeleteAsync(user);
    }
}

/* ---- Reset Password ---- */
public record ResetPasswordCommand(long Id, string NewPassword) : IRequest;

public class ResetPasswordHandler : IRequestHandler<ResetPasswordCommand>
{
    private readonly IUserRepository _userRepo;

    public ResetPasswordHandler(IUserRepository userRepo) => _userRepo = userRepo;

    public async Task Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepo.GetByIdAsync(request.Id);
        if (user == null) throw new InvalidOperationException("用户不存在");

        user.Password = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _userRepo.UpdateAsync(user);
    }
}
