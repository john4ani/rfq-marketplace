using Microsoft.Extensions.Primitives;

namespace Marketplace.Api.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId = ResolveCorrelationId(context.Request.Headers[HeaderName]);
        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        }))
        {
            await next(context);
        }
    }

    private static string ResolveCorrelationId(StringValues values)
    {
        string? supplied = values.FirstOrDefault();

        return !string.IsNullOrWhiteSpace(supplied) && supplied.Length <= 128
            ? supplied
            : Guid.NewGuid().ToString("N");
    }
}
