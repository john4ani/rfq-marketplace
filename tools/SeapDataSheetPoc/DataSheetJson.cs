using System.Text.Json;

namespace SeapDataSheetPoc;

/// <summary>Deterministic JSON writer for the extraction result (temporary POC output, not a persistence schema).</summary>
public static class DataSheetJson
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions Compact = new() { WriteIndented = false };

    public static string Write(DataSheetExtraction result) => JsonSerializer.Serialize(result, Indented);

    public static string Canonical(DataSheetExtraction result) => JsonSerializer.Serialize(result, Compact);
}
