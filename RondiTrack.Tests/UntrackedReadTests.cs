using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace RondiTrack.Tests;

// EDIT 5.2: new file. Proves a PUT still persists now that every read is AsNoTracking
public class UntrackedReadTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Update_User_Persists_After_Untracked_Read()
    {
        var created = await _client.PostAsJsonAsync("/api/users",
            new { FullName = "Before Rename", Email = $"before{Guid.NewGuid()}@example.com" });
        var user = (await created.Content.ReadFromJsonAsync<UserDto>())!;

        var newName = $"Renamed {Guid.NewGuid()}";
        var put = await _client.PutAsJsonAsync($"/api/users/{user.Id}",
            new { FullName = newName, Email = $"after{Guid.NewGuid()}@example.com" });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var get = await _client.GetFromJsonAsync<UserDto>($"/api/users/{user.Id}");
        Assert.Equal(newName, get!.FullName);
    }

    private sealed record UserDto(Guid Id, string FullName);
}