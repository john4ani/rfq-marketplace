namespace Marketplace.Application.AutoBidding;

/// <summary>
/// User-facing auto-bidding pipeline failure (OCR insufficient, truncated LLM response, missing
/// Azure/Claude configuration, external service unavailable). Carries the HTTP status the API should
/// return and a Romanian <see cref="Exception.Message"/> that is safe to show directly to the user.
/// </summary>
public sealed class AutoBiddingPipelineException : InvalidOperationException
{
    public int StatusCode { get; }

    public AutoBiddingPipelineException(int statusCode, string message)
        : base(message) => StatusCode = statusCode;
}
