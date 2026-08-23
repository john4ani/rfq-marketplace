namespace Marketplace.Infrastructure.Documents;

/// <summary>
/// Pure overlapping text chunking. No I/O: directly unit-testable. Splits on paragraph/sentence/word
/// boundaries while keeping a bounded overlap so embeddings retain context across chunk edges.
/// </summary>
public static class TextChunker
{
    public static IReadOnlyCollection<string> Chunk(string text, int chunkSize = 800, int overlap = 120)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        chunkSize = Math.Max(50, chunkSize);
        overlap = Math.Clamp(overlap, 0, chunkSize / 2);
        string clean = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (clean.Length <= chunkSize) return [clean];

        List<string> chunks = [];
        int position = 0;
        while (position < clean.Length)
        {
            int end = Math.Min(position + chunkSize, clean.Length);
            int boundary = FindBoundary(clean, position, end);
            chunks.Add(clean[position..boundary].Trim());
            if (boundary >= clean.Length) break;
            position = Math.Max(position + 1, boundary - overlap);
        }
        return chunks;
    }

    private static int FindBoundary(string text, int start, int end)
    {
        int span = end - start;
        int paragraph = text.LastIndexOf("\n\n", end - 1, span, StringComparison.Ordinal);
        if (paragraph > start + 40) return paragraph;
        int period = text.LastIndexOf(". ", end - 1, span, StringComparison.Ordinal);
        if (period > start + 40) return period + 2;
        int space = text.LastIndexOf(' ', end - 1, span);
        return space > start + 40 ? space : end;
    }
}
