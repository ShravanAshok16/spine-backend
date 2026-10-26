using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Spine.Api.Models;

namespace Spine.Tests.Integration;

public class AuthFlowTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public AuthFlowTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _factory = factory;
    }

    [Fact]
    public async Task Register_WithNewEmail_Returns200()
    {
        // Arrange
        var email = $"newuser_{Guid.NewGuid()}@example.com";
        var request = new RegisterRequest
        {
            Email = email,
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            FirstName = "John",
            LastName = "Doe",
            PhoneNumber = "1234567890"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/Auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert the response contains the message, not a token
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Registration successful");
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns400()
    {
        // Arrange
        var email = $"duplicate_{Guid.NewGuid()}@example.com";
        var request = new RegisterRequest
        {
            Email = email,
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            FirstName = "Jane",
            LastName = "Doe",
            PhoneNumber = "0987654321"
        };

        // Register first time
        await _client.PostAsJsonAsync("/api/Auth/register", request);

        // Act - Register second time
        var response = await _client.PostAsJsonAsync("/api/Auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsToken()
    {
        // Arrange
        var email = $"login_{Guid.NewGuid()}@example.com";
        await TestHelpers.RegisterAndGetTokenAsync(_client, _factory, email);

        var loginRequest = new LoginRequest
        {
            Identifier = email,
            Password = "Password123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/Auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        result!.Token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        // Arrange
        var email = $"wrongpass_{Guid.NewGuid()}@example.com";
        await TestHelpers.RegisterAndGetTokenAsync(_client, _factory, email);

        var loginRequest = new LoginRequest
        {
            Identifier = email,
            Password = "WrongPassword123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/Auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}