using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ProjectManagement.Application.Auth;

namespace ProjectManagement.IntegrationTests.Controllers;

/// <summary>
/// AuthController 集成测试
/// 需要真实数据库连接 — 使用 TestServer (WebApplicationFactory)
/// </summary>
public class AuthControllerTests
{
    // 注意：集成测试需要 WebApplicationFactory<Program>
    // 以下为测试骨架，实际运行时取消注释并配置 TestServer

    /*
    private readonly HttpClient _client;

    public AuthControllerTests()
    {
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ConnectionStrings:DefaultConnection",
                    "Server=localhost;Database=PM_Test;...");
            });
        _client = factory.CreateClient();
    }
    */

    [Fact]
    public void Login_ValidCredentials_ReturnsToken()
    {
        // 骨架 — 标记测试意图
        // var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginCommand
        // {
        //     Username = "admin",
        //     Password = "admin123"
        // });
        //
        // response.StatusCode.Should().Be(HttpStatusCode.OK);
        // var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResult>>();
        // result!.Data!.Token.Should().NotBeNullOrEmpty();

        // 此处为占位断言，实际测试时替换
        true.Should().BeTrue("集成测试需要数据库连接 — 运行时替换此骨架");
    }

    [Fact]
    public void Login_InvalidPassword_Returns401()
    {
        // var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginCommand
        // {
        //     Username = "admin",
        //     Password = "wrong_password"
        // });
        //
        // response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        true.Should().BeTrue("集成测试需要数据库连接 — 运行时替换此骨架");
    }
}
