namespace RondiTrack.Tests.TestSupport;                       // folder is TestSupport, NOT Infrastructure

using Microsoft.AspNetCore.Hosting;                           // IWebHostBuilder
using Microsoft.AspNetCore.Mvc.Testing;                       // WebApplicationFactory
using Microsoft.EntityFrameworkCore;                          // UseNpgsql, MigrateAsync
using RondiTrack.Data;                                        // RondiTrackDbContext
using Testcontainers.PostgreSql;                              // the PostgreSQL container

public class PostgresApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17")                             // Postgres version from the assignment
        .WithDatabase("ronditrack_test")                      // test database name
        .WithUsername("postgres")                             // test username
        .WithPassword("postgres")                             // test password
        .Build();                                             // create the container object

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
        // UseSetting (NOT ConfigureAppConfiguration) so Program.cs sees it at startup.
        // "RondiTrack" is the exact key your Program.cs reads.
        builder.UseSetting("ConnectionStrings:RondiTrack", _container.GetConnectionString());
    }

    async Task IAsyncLifetime.DisposeAsync() => await _container.DisposeAsync(); // remove container after tests
}

[CollectionDefinition("Postgres collection")]                 // name the test classes use
public class PostgresCollection : ICollectionFixture<PostgresApiFactory> { } // one shared container for all tests
