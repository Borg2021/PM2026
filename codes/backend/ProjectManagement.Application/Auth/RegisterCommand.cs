using MediatR;

namespace ProjectManagement.Application.Auth;

public record RegisterCommand(string Username, string Password, string RealName, string Role) : IRequest<RegisterResult>;

public record RegisterResult(bool Success, string? Message = null);
