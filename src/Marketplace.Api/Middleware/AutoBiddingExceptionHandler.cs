using Marketplace.Application.AutoBidding;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.Api.Middleware;

/// <summary>
/// Converts domain exceptions that carry a user-facing message into ProblemDetails responses so the SPA
/// can show a specific, actionable error (e.g. "PDF-ul pare scanat ilizibil sau protejat…") instead of a
/// generic 500. Unknown exceptions are left to the default handler (never leak internal details).
/// </summary>
public sealed class AutoBiddingExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not AutoBiddingPipelineException pipeline)
            return false;

        ProblemDetails problem = new()
        {
            Status = pipeline.StatusCode,
            Title = "Pipeline-ul nu a putut rula.",
            Detail = pipeline.Message
        };
        httpContext.Response.StatusCode = pipeline.StatusCode;
        httpContext.Response.ContentType = "application/problem+json";
        if (httpContext.Response.HasStarted)
            return false;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
