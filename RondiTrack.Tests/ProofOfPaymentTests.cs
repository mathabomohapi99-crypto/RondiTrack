using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using RondiTrack.Domain;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

[Collection("Postgres collection")]
public class ProofOfPaymentTests(PostgresApiFactory factory)
{
    private IServiceProvider Sp => factory.Services;

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

    private static MultipartFormDataContent Form(byte[] bytes, string fileName, string contentType)
    {
        var form = new MultipartFormDataContent();
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(part, "file", fileName);
        return form;
    }

    private static byte[] Pdf(int size)
    {
        var bytes = new byte[size];
        Encoding.ASCII.GetBytes("%PDF-1.4\n").CopyTo(bytes, 0);
        return bytes;
    }

    private static string Url(Guid stokvelId, Guid contributionId) => $"/api/v1/stokvels/{stokvelId}/contributions/{contributionId}/proof";

    // Stokvel A: Thandi (member), Sipho (TREASURER), Naledi (member). Stokvel B: Sipho (MEMBER), Kagiso (TREASURER).
    private sealed record World(Guid A, Guid CycleA, Guid Thandi, Guid Sipho, Guid Naledi, Guid B, Guid Kagiso, Guid ContribA, Guid ContribB);

    private async Task<World> BuildAsync()
    {
        var thandi = await TestSeed.UserAsync(Sp); var sipho = await TestSeed.UserAsync(Sp);
        var naledi = await TestSeed.UserAsync(Sp); var kagiso = await TestSeed.UserAsync(Sp);
        var (a, ca) = await TestSeed.StokvelAsync(Sp, [(thandi, StokvelRole.Member), (sipho, StokvelRole.Treasurer), (naledi, StokvelRole.Member)]);
        var (b, cb) = await TestSeed.StokvelAsync(Sp, [(sipho, StokvelRole.Member), (kagiso, StokvelRole.Treasurer)]);
        var contribA = await W9.AddContributionAsync(a, thandi, ca[0], 500m);
        var contribB = await W9.AddContributionAsync(b, kagiso, cb[0], 500m);
        return new World(a, ca[0], thandi, sipho, naledi, b, kagiso, contribA, contribB);
    }

    [Fact]
    public async Task Only_the_uploader_and_treasurers_of_that_stokvel_can_download()
    {
        var w = await BuildAsync();
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClientFor(w.Thandi).PutAsync(Url(w.A, w.ContribA), Form(Png, "slip.png", "image/png"))).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClientFor(w.Thandi).GetAsync(Url(w.A, w.ContribA))).StatusCode);     // the uploader
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClientFor(w.Sipho).GetAsync(Url(w.A, w.ContribA))).StatusCode);      // treasurer of A
        var member = await factory.CreateClientFor(w.Naledi).GetAsync(Url(w.A, w.ContribA));                                       // ordinary member of A
        var otherTreasurer = await factory.CreateClientFor(w.Kagiso).GetAsync(Url(w.A, w.ContribA));                                // treasurer of B only
        var missing = await factory.CreateClientFor(w.Naledi).GetAsync(Url(w.A, Guid.NewGuid()));                                   // a file that does not exist
        Assert.Equal(HttpStatusCode.NotFound, member.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, otherTreasurer.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        // all three "no" answers are IDENTICAL, so nobody can tell "exists but not yours" from "does not exist"
        Assert.Equal(await ProblemJson.ReadAsync(missing), await ProblemJson.ReadAsync(member));
        Assert.Equal(await ProblemJson.ReadAsync(missing), await ProblemJson.ReadAsync(otherTreasurer));
    }

    [Fact]
    public async Task One_person_two_roles_Sipho_opens_proofs_where_he_is_treasurer_only()
    {
        var w = await BuildAsync();
        await factory.CreateClientFor(w.Thandi).PutAsync(Url(w.A, w.ContribA), Form(Png, "a.png", "image/png"));
        await factory.CreateClientFor(w.Kagiso).PutAsync(Url(w.B, w.ContribB), Form(Png, "b.png", "image/png"));

        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClientFor(w.Sipho).GetAsync(Url(w.A, w.ContribA))).StatusCode);         // treasurer in A
        Assert.Equal(HttpStatusCode.NotFound, (await factory.CreateClientFor(w.Sipho).GetAsync(Url(w.B, w.ContribB))).StatusCode);   // only a member in B
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClientFor(w.Kagiso).GetAsync(Url(w.B, w.ContribB))).StatusCode);
    }

    [Fact]
    public async Task The_type_is_judged_by_the_bytes_and_the_limit_by_the_size()
    {
        var w = await BuildAsync();
        var client = factory.CreateClientFor(w.Thandi);

        // a text file renamed .pdf, with a PDF content type: refused, message on the "file" field
        var fake = await client.PutAsync(Url(w.A, w.ContribA), Form(Encoding.UTF8.GetBytes("just some text"), "slip.pdf", "application/pdf"));
        Assert.Equal(HttpStatusCode.BadRequest, fake.StatusCode);
        Assert.True((await fake.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("file", out _));

        // 5 MB + 1 byte: too large
        var tooBig = await client.PutAsync(Url(w.A, w.ContribA), Form(Pdf(5 * 1024 * 1024 + 1), "big.pdf", "application/pdf"));
        Assert.Equal(HttpStatusCode.BadRequest, tooBig.StatusCode);
        Assert.True((await tooBig.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("file", out _));

        // just under the limit uploads fine
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsync(Url(w.A, w.ContribA), Form(Pdf(5 * 1024 * 1024 - 1000), "big.pdf", "application/pdf"))).StatusCode);

        // a real PNG sent with the WRONG content type is still accepted and served as image/png
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsync(Url(w.A, w.ContribA), Form(Png, "slip.txt", "text/plain"))).StatusCode);
        var download = await client.GetAsync(Url(w.A, w.ContribA));
        Assert.Equal("image/png", download.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task The_download_is_an_attachment_that_is_never_sniffed_or_cached()
    {
        var w = await BuildAsync();
        await factory.CreateClientFor(w.Thandi).PutAsync(Url(w.A, w.ContribA), Form(Png, "slip.png", "image/png"));

        var res = await factory.CreateClientFor(w.Thandi).GetAsync(Url(w.A, w.ContribA));
        Assert.Equal("nosniff", res.Headers.GetValues("X-Content-Type-Options").Single());
        var cache = res.Headers.CacheControl!;
        Assert.True(cache.NoStore);
        Assert.True(cache.Private);
        Assert.Equal("attachment", res.Content.Headers.ContentDisposition?.DispositionType);
    }

    [Fact]
    public async Task Once_the_cycle_is_closed_the_proof_is_locked()
    {
        var w = await BuildAsync();
        var client = factory.CreateClientFor(w.Thandi);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsync(Url(w.A, w.ContribA), Form(Png, "slip.png", "image/png"))).StatusCode);

        await W9.CloseCycleDirectlyAsync(w.CycleA);

        var replace = await client.PutAsync(Url(w.A, w.ContribA), Form(Png, "new.png", "image/png"));
        Assert.Equal(HttpStatusCode.Conflict, replace.StatusCode);
        Assert.Equal("proof_locked", (await replace.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync(Url(w.A, w.ContribA))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Url(w.A, w.ContribA))).StatusCode);   // still readable
    }

    [Fact]
    public async Task The_list_shows_each_person_only_the_proofs_they_may_open()
    {
        var w = await BuildAsync();
        await factory.CreateClientFor(w.Thandi).PutAsync(Url(w.A, w.ContribA), Form(Png, "slip.png", "image/png"));
        var url = $"/api/v1/stokvels/{w.A}/proofs";

        Assert.Equal(1, (await factory.CreateClientFor(w.Sipho).GetFromJsonAsync<JsonElement>(url)).GetArrayLength());    // treasurer sees it
        Assert.Equal(1, (await factory.CreateClientFor(w.Thandi).GetFromJsonAsync<JsonElement>(url)).GetArrayLength());   // the owner sees it
        Assert.Equal(0, (await factory.CreateClientFor(w.Naledi).GetFromJsonAsync<JsonElement>(url)).GetArrayLength());   // another member does not
        Assert.Equal(HttpStatusCode.NotFound, (await factory.CreateClientFor(w.Kagiso).GetAsync(url)).StatusCode);        // not in this stokvel
    }
}