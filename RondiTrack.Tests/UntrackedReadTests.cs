using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace RondiTrack.Tests;

// EDIT 5.2: new file. Proves a PUT still persists now that every read is AsNoTracking
// EDIT 5.3: the PUT must now send If-Match (the ETag from the POST/GET), otherwise it is a 428.
public class UntrackedReadTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Update_User_Persists_After_Untracked_Read()
    {
        var created = await _client.PostAsJsonAsync("/api/users",
            new { FullName = "Before Rename", Email = $"before{Guid.NewGuid()}@example.com" });
        var user = (await created.Content.ReadFromJsonAsync<UserDto>())!;
        var etag = created.Headers.ETag!.Tag;   // EDIT 5.3: the token comes back with the response

        var newName = $"Renamed {Guid.NewGuid()}";
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/users/{user.Id}")
        {
            Content = JsonContent.Create(new { FullName = newName, Email = $"after{Guid.NewGuid()}@example.com" })
        };
        request.Headers.TryAddWithoutValidation("If-Match", etag);   // EDIT 5.3
        var put = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var get = await _client.GetFromJsonAsync<UserDto>($"/api/users/{user.Id}");
        Assert.Equal(newName, get!.FullName);
    }

    private sealed record UserDto(Guid Id, string FullName);
}