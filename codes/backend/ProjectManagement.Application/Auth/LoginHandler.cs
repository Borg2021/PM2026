using MediatR;
using ProjectManagement.Domain.Interfaces;

namespace ProjectManagement.Application.Auth;

public class LoginHandler : IRequestHandler<LoginCommand, LoginResult>
{
    private readonly IUserRepository _userRepo;

    public LoginHandler(IUserRepository userRepo) => _userRepo = userRepo;

    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepo.GetByUsernameAsync(request.Username);
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
            return new LoginResult(false, Message: "用户名或密码错误");

        return new LoginResult(true, RealName: user.RealName, Role: user.Role, UserId: user.Id);
    }
}
