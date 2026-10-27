using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RondiTrack.Domain;
using RondiTrack.Errors;
using RondiTrack.Tests.TestSupport;
using Xunit;

namespace RondiTrack.Tests;

// NEW 6.2: the handler's logging rules, tested without HTTP or a database
public class ExceptionHandlerLoggingTests
{
    // builds the real handler with our capturing logger
    private static (RondiExceptionHandler Handler, CapturingLoggerProvider Logs) Build()
    {
        var logs = new CapturingLoggerProvider();
        var loggerFactory = LoggerFactory.Create(b => b.AddProvider(logs));
        return (new RondiExceptionHandler(loggerFactory.CreateLogger<RondiExceptionHandler>()), logs);
    }

    [Fact]
    public async Task A_4xx_Is_Logged_At_Information_With_The_Type_But_Not_The_Message()
    {
        var (handler, logs) = Build();
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();   // the handler writes the response here

        await handler.TryHandleAsync(context, new DomainConflictException("secret-person@example.com"), CancellationToken.None);

        var entry = Assert.Single(logs.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);                       // 4xx = Information
        Assert.Contains("DomainConflictException", entry.Message);             // the type is logged
        Assert.DoesNotContain("secret-person@example.com", entry.AllText);    // the message is not
        Assert.Null(entry.Exception);                                          // no stack trace for a 4xx
    }

    [Fact]
    public async Task A_5xx_Is_Logged_At_Error_With_The_Exception_And_The_Client_Gets_A_Generic_Detail()
    {
        var (handler, logs) = Build();
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await handler.TryHandleAsync(context, new InvalidOperationException("internal-detail"), CancellationToken.None);

        var entry = Assert.Single(logs.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);                  // 5xx = Error
        Assert.Contains("internal-detail", entry.Exception);        // the full exception is in the log

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.DoesNotContain("internal-detail", body);             // but never in the response
        Assert.Contains("An unexpected error occurred.", body);     // generic detail
        Assert.Contains("correlationId", body);                     // plus the id to quote
    }
}