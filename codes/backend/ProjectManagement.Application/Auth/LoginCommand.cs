using MediatR;

namespace ProjectManagement.Application.Auth;

public record LoginCommand(string Username, string Password) : IRequest<LoginResult>;

public record LoginResult(bool Success, string? Token = null, string? Message = null, string? RealName = null, string? Role = null, long UserId = 0);
