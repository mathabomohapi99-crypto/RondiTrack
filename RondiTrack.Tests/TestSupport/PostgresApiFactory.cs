namespace RondiTrack.Tests.TestSupport;                       // folder is TestSupport, NOT Infrastructure

using System.Net.Http.Headers;                                // AuthenticationHeaderValue
using System.Security.Cryptography;                           // random test signing key
using Microsoft.AspNetCore.Hosting;                           // IWebHostBuilder
using Microsoft.AspNetCore.Mvc.Testing;                       // WebApplicationFactory
using Microsoft.EntityFrameworkCore;                          // UseNpgsql, MigrateAsync
using Microsoft.Extensions.DependencyInjection;               // GetRequiredService
using Microsoft.Extensions.Logging;                           // NEW 6.2: needed for AddProvider
using RondiTrack.Auth;                                        // ITokenService
using RondiTrack.Data;                                        // RondiTrackDbContext
using Testcontainers.PostgreSql;                              // the PostgreSQL container

public class PostgresApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17")   // CHANGED: image goes in the constructor (removes the obsolete warning)
        .WithDatabase("ronditrack_test")                      // test database name
        .WithUsername("postgres")                             // test username
        .WithPassword("postgres")                             // test password
        .Build();                                             // create the container object

    // NEW 6.1: a random signing key made fresh on every run. Nothing is committed.
    private readonly string _jwtKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    // NEW 6.2: every log line the app writes during a test run ends up here
    public CapturingLoggerProvider CapturedLogs { get; } = new();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();                        // start the Docker container first

        TestDb.ConnectionString = _container.GetConnectionString();   // TestDb helpers use the container too

        // Program.cs seeds data at startup (Development), so the tables must exist BEFORE the app starts.
        var options = new DbContextOptionsBuilder<RondiTrackDbContext>()
            .UseNpgsql(_container.GetConnectionString())      // point at the container
            .Options;                                         // build the options
        await using var db = new RondiTrackDbContext(options); // temporary context just for migrating
        await db.Database.MigrateAsync();                     // create all tables in the empty container
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureLogging(logging => logging.AddProvider(CapturedLogs));   // NEW 6.2: capture logs for the leak test

        // UseSetting (NOT ConfigureAppConfiguration) so Program.cs sees it at startup.
        // "RondiTrack" is the exact key your Program.cs reads.
        builder.UseSetting("ConnectionStrings:RondiTrack", _container.GetConnectionString());
        builder.UseSetting("Jwt:SigningKey", _jwtKey);        // NEW 6.1: the app refuses to start without it
    }

    // NEW 6.1: every client made with CreateClient() starts signed in as a platform admin.
    // That is how the old tests (which know nothing about auth) keep passing without any change.
    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", MintToken(Guid.NewGuid(), platformAdmin: true));
    }

    // NEW 6.1: makes a real access token with the app's own TokenService (same key, same validation path)
    public string MintToken(Guid userId, bool platformAdmin = false) =>
        Services.GetRequiredService<ITokenService>().CreateAccessToken(userId, platformAdmin).Value;

    // NEW 6.1: a client signed in as one specific (non-admin) user
    public HttpClient CreateClientFor(Guid userId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", MintToken(userId));
        return client;
    }

    // NEW 6.1: a client with no token. Uses https and no cookie handling, so tests read Set-Cookie by hand.
    public HttpClient CreateAnonymousClient()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false,
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Authorization = null;
        return client;
    }

    async Task IAsyncLifetime.DisposeAsync() => await _container.DisposeAsync(); // remove container after tests
}

[CollectionDefinition("Postgres collection")]                 // name the test classes use
public class PostgresCollection : ICollectionFixture<PostgresApiFactory> { } // one shared container for all tests