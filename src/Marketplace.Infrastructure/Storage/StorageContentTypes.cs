namespace Marketplace.Infrastructure.Storage;

/// <summary>Maps persisted file names to content types for download responses.</summary>
public static class StorageContentTypes
{
    public static string FromFileName(string fileName)
    {
        string extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".xls" => "application/vnd.ms-excel",
            _ => "application/octet-stream"
        };
    }
}
