using MediatR;
using ProjectManagement.Domain.Entities;
using ProjectManagement.Domain.Interfaces;

namespace ProjectManagement.Application.Auth;

public class RegisterHandler : IRequestHandler<RegisterCommand, RegisterResult>
{
    private readonly IUserRepository _userRepo;

    public RegisterHandler(IUserRepository userRepo) => _userRepo = userRepo;

    public async Task<RegisterResult> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        if (await _userRepo.ExistsAsync(request.Username))
            return new RegisterResult(false, "用户名已存在");

        await _userRepo.AddAsync(new User
        {
            Username = request.Username,
            Password = BCrypt.Net.BCrypt.HashPassword(request.Password),
            RealName = request.RealName,
            Role = request.Role,
            Status = 1,
            CreatedAt = DateTime.UtcNow
        });
        return new RegisterResult(true);
    }
}
