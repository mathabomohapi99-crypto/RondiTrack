using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace RondiTrack.Tests;

// NEW 5.3: proves the paging contract through the real HTTP endpoint.
public class PagingTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    // Builds a stokvel with 3 members who each contributed once. Returns the list URL.
    private async Task<string> SeedThreeContributionsAsync()
    {
        var stokvel = (await (await _client.PostAsJsonAsync("/api/stokvels",
            new { Name = $"Paging {Guid.NewGuid()}", ContributionAmount = 100m, Frequency = 1, MaxMembers = 5 }))
            .Content.ReadFromJsonAsync<IdDto>())!;

        var cycle = (await (await _client.PostAsJsonAsync($"/api/stokvels/{stokvel.Id}/cycles",
            new { CycleNumber = 1, TargetAmount = 100m })).Content.ReadFromJsonAsync<IdDto>())!;

        for (var i = 0; i < 3; i++)
        {
            var user = (await (await _client.PostAsJsonAsync("/api/users",
                new { FullName = $"Pager {i}", Email = $"pager{Guid.NewGuid()}@example.com" }))
                .Content.ReadFromJsonAsync<IdDto>())!;
            await _client.PostAsJsonAsync($"/api/stokvels/{stokvel.Id}/members", new { UserId = user.Id });

            var request = new HttpRequestMessage(HttpMethod.Post, $"/api/stokvels/{stokvel.Id}/contributions")
            {
                Content = JsonContent.Create(new { UserId = user.Id, CycleId = cycle.Id, Amount = 100m })
            };
            request.Headers.Add("Idempotency-Key", $"k-{Guid.NewGuid()}");
            await _client.SendAsync(request);
        }

        return $"/api/stokvels/{stokvel.Id}/cycles/{cycle.Id}/contributions";
    }

    [Fact]
    public async Task Walking_all_pages_returns_every_row_once_and_ends_with_an_empty_token()
    {
        var url = await SeedThreeContributionsAsync();
        var seen = new List<Guid>();
        var token = "";

        do
        {
            var query = $"{url}?pageSize=2" + (token == "" ? "" : $"&pageToken={Uri.EscapeDataString(token)}");
            var page = (await _client.GetFromJsonAsync<PageDto>(query))!;
            seen.AddRange(page.Items.Select(i => i.Id));
            token = page.NextPageToken;
        } while (token != "");

        Assert.Equal(3, seen.Count);
        Assert.Equal(3, seen.Distinct().Count());
    }

    [Fact]
    public async Task Negative_page_size_returns_400()
    {
        var url = await SeedThreeContributionsAsync();
        var response = await _client.GetAsync($"{url}?pageSize=-1");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Unknown_sort_field_returns_400()
    {
        var url = await SeedThreeContributionsAsync();
        var response = await _client.GetAsync($"{url}?sort=memberName");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Token_reused_with_a_different_sort_returns_400()
    {
        var url = await SeedThreeContributionsAsync();
        var first = (await _client.GetFromJsonAsync<PageDto>($"{url}?pageSize=1"))!;
        Assert.NotEqual("", first.NextPageToken);

        var response = await _client.GetAsync(
            $"{url}?pageSize=1&sort=-date&pageToken={Uri.EscapeDataString(first.NextPageToken)}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed record IdDto(Guid Id);
    private sealed record PageDto(List<IdDto> Items, string NextPageToken);
}