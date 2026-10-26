using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Spine.Api.Models;
using Spine.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Spine.Tests.Integration;

public static class TestHelpers
{
    public static async Task<string> RegisterAndGetTokenAsync(
        HttpClient client, 
        CustomWebApplicationFactory factory, // Pass factory to access DB
        string email)
    {
        var request = new RegisterRequest
        {
            Email = email,
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            FirstName = "Test",
            LastName = "User",
            PhoneNumber = "1234567890",
            DisplayName = "Test User"
        };

        var response = await client.PostAsJsonAsync("/api/Auth/register", request);
        response.EnsureSuccessStatusCode();

        // 1. Bypass verification by updating the DB directly
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user != null)
        {
            user.IsEmailVerified = true;
            await db.SaveChangesAsync();
        }

        // 2. Now login to get the token
        var loginRequest = new LoginRequest { Identifier = email, Password = "Password123!" };
        var loginResponse = await client.PostAsJsonAsync("/api/Auth/login", loginRequest);
        loginResponse.EnsureSuccessStatusCode();

        var authResponse = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        return authResponse!.Token;
    }

    public static void AuthenticateClient(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}