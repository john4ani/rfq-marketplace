using System.Text.Json;

namespace Marketplace.Infrastructure.Bidding;

/// <summary>Parses the Anthropic Messages response body used by the auto-bid pipeline.</summary>
public static class ClaudeResponse
{
    /// <summary>
    /// Returns the first text content block. Models with extended thinking (e.g. claude-sonnet-5) emit
    /// "thinking" blocks before the text block, so content[0] is not always the answer — find the text.
    /// </summary>
    public static string ExtractText(string body)
    {
        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement content = document.RootElement.GetProperty("content");
        foreach (JsonElement block in content.EnumerateArray())
        {
            if (block.TryGetProperty("type", out JsonElement type) && type.GetString() == "text"
                && block.TryGetProperty("text", out JsonElement textElement)
                && textElement.GetString() is { Length: > 0 } text)
            {
                return text;
            }
        }
        throw new InvalidOperationException("Răspunsul Claude nu conține text.");
    }
}
