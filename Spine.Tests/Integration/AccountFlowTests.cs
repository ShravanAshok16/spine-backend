using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Spine.Api.Models;

namespace Spine.Tests.Integration;

public class AccountFlowTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public AccountFlowTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _factory = factory;
    }

    [Fact]
    public async Task GetMe_WithoutToken_Returns401()
    {
        // Act
        var response = await _client.GetAsync("/api/Account/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMe_WithToken_Returns200()
    {
        // Arrange
        var email = $"getme_{Guid.NewGuid()}@example.com";
        var token = await TestHelpers.RegisterAndGetTokenAsync(_client, _factory, email);
        TestHelpers.AuthenticateClient(_client, token);

        // Act
        var response = await _client.GetAsync("/api/Account/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(email);
    }

    [Fact]
    public async Task UpdatePassword_WithWrongCurrentPassword_Returns400()
    {
        // Arrange
        var email = $"badpass_{Guid.NewGuid()}@example.com";
        var token = await TestHelpers.RegisterAndGetTokenAsync(_client, _factory, email);
        TestHelpers.AuthenticateClient(_client, token);

        var request = new UpdatePasswordRequest
        {
            CurrentPassword = "WrongPassword123!",
            NewPassword = "NewPassword123!",
            ConfirmPassword = "NewPassword123!"
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/Account/password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdatePassword_Valid_Returns204AndAllowsLogin()
    {
        // Arrange
        var email = $"update_{Guid.NewGuid()}@example.com";
        var token = await TestHelpers.RegisterAndGetTokenAsync(_client, _factory, email);
        TestHelpers.AuthenticateClient(_client, token);

        var updateRequest = new UpdatePasswordRequest
        {
            CurrentPassword = "Password123!",
            NewPassword = "NewPassword123!",
            ConfirmPassword = "NewPassword123!"
        };

        // Act - Update Password
        var response = await _client.PutAsJsonAsync("/api/Account/password", updateRequest);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act - Try to login with NEW password
        var loginRequest = new LoginRequest { Identifier = email, Password = "NewPassword123!" };
        var loginResponse = await _client.PostAsJsonAsync("/api/Auth/login", loginRequest);

        // Assert
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}