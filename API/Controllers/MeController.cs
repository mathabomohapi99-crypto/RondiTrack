[Route($"{ApiRoutes.V1}/me")]
public sealed class MeController(IHomeService home) : RondiControllerBase
{
    [HttpGet("home")]
    public async Task<ActionResult<HomeResponse>> Home(CancellationToken ct) =>
        Ok(await home.GetAsync(User.GetUserId(), ct));
}

[Fact]
public async Task Member_of_two_stokvels_sees_both_with_correct_roles() { /* seed user as Treasurer in A, Member in B → GET /api/v1/me/home → 2 items, roles "Treasurer" and "Member" */ }

[Fact]
public async Task User_in_no_stokvels_gets_empty_list_not_404()
{
    var res = await client.GetAsync("/api/v1/me/home");
    Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
    Assert.Equal(0, doc.RootElement.GetProperty("stokvels").GetArrayLength());
}