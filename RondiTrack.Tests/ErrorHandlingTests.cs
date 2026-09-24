using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace RondiTrack.Tests;

public class ErrorHandlingTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Create_User_With_Empty_Name_Returns_400_ProblemJson()
    {
        var response = await _client.PostAsJsonAsync("/api/users", new { FullName = "", Email = "test@example.com" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Get_User_That_Does_Not_Exist_Returns_404_ProblemJson()
    {
        var response = await _client.GetAsync($"/api/users/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Create_User_With_Duplicate_Email_Returns_409_ProblemJson()
    {
        var payload = new { FullName = "Test Person", Email = "thandi.nkosi@example.com" };

        var response = await _client.PostAsJsonAsync("/api/users", payload);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}