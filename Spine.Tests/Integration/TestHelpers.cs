using System.Net.Http.Headers;
using System.Net.Http.Json;
using Spine.Api.Models;

namespace Spine.Tests.Integration;

public static class TestHelpers
{
    public static async Task<string> RegisterAndGetTokenAsync(HttpClient client, string email)
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

        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return authResponse!.Token;
    }

    public static void AuthenticateClient(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}