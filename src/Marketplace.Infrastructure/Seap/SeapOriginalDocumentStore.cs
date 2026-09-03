using System.Security.Cryptography;
using Marketplace.Application.Seap;
using Marketplace.Infrastructure.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Marketplace.Infrastructure.Seap;

/// <summary>
/// Stage 1 (DocumentImport) dossier storage for the ORIGINAL published documents of a Direct Hit tender —
/// deliberately separate from <see cref="SeapAttachmentStorage"/>. Files are persisted under their own
/// logically separate namespace (<c>seap-originals/{tenderId}</c>), never mixed with user uploads
/// (<c>vault/…</c>) or with the on-demand browsing copies (<c>seap/{tenderId}</c>), so Stage 2/3 later ground
/// themselves in a stable dossier even when e-licitatie.ro becomes temporarily unavailable or replaces a file.
/// The SHA-256 content digest is computed from the REAL stored bytes while streaming (no full-file buffering).
/// Per-file size limits (<c>Seap:MaxOriginalDocumentBytes</c>) are enforced from the declared length AND again
/// while streaming; an over-limit upload is aborted with no partial object left behind.
/// </summary>
public sealed partial class SeapOriginalDocumentStore(
    IStorageService storage,
    IOptions<SeapStorageOptions> options,
    IOptions<LocalStorageOptions> localStorage,
    IOptions<SeapOptions> seapOptions,
    ILogger<SeapOriginalDocumentStore> logger) : ISeapOriginalDocumentStore
{
    private readonly SeapStorageOptions settings = options.Value;
    private readonly SeapOptions seap = seapOptions.Value;
    private readonly bool useLocalStorage = localStorage.Value.UseLocalStorage;

    public async Task<StoredSeapOriginalDocument> StoreOriginalAsync(
        Guid tenderId, string fileName, Stream content, CancellationToken cancellationToken)
    {
        if (!useLocalStorage && (!settings.Enabled || string.IsNullOrWhiteSpace(settings.Bucket)))
            throw new InvalidOperationException("Stocarea dosarelor SEAP (Direct Hit) nu este configurată.");

        long maxBytes = seap.MaxOriginalDocumentBytes;
        // Content-Length pre-check when the source declares its length (HTTP Content-Length, file streams, …):
        // fail before anything is uploaded.
        if (maxBytes > 0 && content.CanSeek && content.Length > maxBytes)
            throw new SeapOriginalDocumentTooLargeException(content.Length, maxBytes);

        Stream uploadSource = content;
        // Enforce the per-file cap AGAIN while the storage layer copies the bytes, so a chunked download with no
        // declared length can never slip past the guard. The wrapped stream throws as soon as the cap is crossed
        // and the storage implementations remove any partial object before rethrowing.
        if (maxBytes > 0)
            uploadSource = new MaxLengthStream(content, maxBytes);

        var hashing = new ContentHashingStream(uploadSource);
        string reference;
        try
        {
            reference = await storage.UploadAsync(
                settings.Bucket, $"seap-originals/{tenderId}", Sanitize(fileName), hashing,
                SeapAttachmentStorage.DetectContentType(fileName), cancellationToken);
        }
        finally
        {
            // Disposing finalizes the incremental hash (computes Sha256Hex from the real stored bytes).
            hashing.Dispose();
            uploadSource.Dispose();
        }
        LogSeapOriginalStored(logger, useLocalStorage);
        return new StoredSeapOriginalDocument(reference, hashing.Sha256Hex, hashing.TotalBytes);
    }

    public async Task<SeapOriginalDocumentContent?> OpenOriginalAsync(string reference, CancellationToken cancellationToken)
    {
        StorageObject? stored = await storage.OpenReadAsync(reference, cancellationToken);
        return stored is null ? null : new SeapOriginalDocumentContent(stored.Content, stored.ContentType);
    }

    public Task DeleteOriginalAsync(string reference, CancellationToken cancellationToken)
    {
        LogSeapOriginalDeleted(logger, useLocalStorage);
        return storage.DeleteReferenceAsync(reference, cancellationToken);
    }

    private static string Sanitize(string fileName) =>
        string.Join('_', fileName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Stream wrapper that throws <see cref="SeapOriginalDocumentTooLargeException"/> as soon as more
    /// than <paramref name="maxBytes"/> were read from the inner stream — the storage layer aborts the upload
    /// (removing any partially written object), so an over-limit file is never durably persisted.</summary>
    private sealed class MaxLengthStream(Stream inner, long maxBytes) : Stream
    {
        private long total;

        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = inner.Read(buffer, offset, count);
            return Guard(read);
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int read = await inner.ReadAsync(buffer, cancellationToken);
            return Guard(read);
        }

        public override int ReadByte()
        {
            int value = inner.ReadByte();
            if (value >= 0)
            {
                total += 1;
                if (total > maxBytes) throw TooLarge();
            }
            return value;
        }

        private int Guard(int read)
        {
            if (read > 0)
            {
                total += read;
                if (total > maxBytes) throw TooLarge();
            }
            return read;
        }

        private SeapOriginalDocumentTooLargeException TooLarge() =>
            new(total, maxBytes);

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>Stream wrapper that captures the SHA-256 and the byte count of everything read from the inner
    /// stream (the storage layer copies the upload through <see cref="ReadAsync(Memory{byte},CancellationToken)"/>),
    /// so the returned digest always matches the bytes that were actually persisted.</summary>
    private sealed class ContentHashingStream(Stream inner) : Stream
    {
        private readonly IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        private bool finalized;

        public string Sha256Hex { get; private set; } = string.Empty;
        public long TotalBytes { get; private set; }

        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => inner.Length;

        public override long Position
        {
            get => inner.Position;
            set => throw new NotSupportedException();
        }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = inner.Read(buffer, offset, count);
            if (read > 0)
            {
                hash.AppendData(buffer, offset, read);
                TotalBytes += read;
            }
            return read;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int read = await inner.ReadAsync(buffer, cancellationToken);
            if (read > 0)
            {
                hash.AppendData(buffer.Span[..read]);
                TotalBytes += read;
            }
            return read;
        }

        public override int ReadByte()
        {
            int value = inner.ReadByte();
            if (value >= 0)
            {
                hash.AppendData([(byte)value]);
                TotalBytes += 1;
            }
            return value;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing && !finalized)
            {
                finalized = true;
                Sha256Hex = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
                hash.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    [LoggerMessage(EventId = 5150, Level = LogLevel.Information, Message = "SEAP original document stored in the Direct Hit dossier (local={UseLocalStorage}).")]
    private static partial void LogSeapOriginalStored(ILogger logger, bool useLocalStorage);
    [LoggerMessage(EventId = 5153, Level = LogLevel.Information, Message = "SEAP dossier original deleted from the Direct Hit dossier store (local={UseLocalStorage}).")]
    private static partial void LogSeapOriginalDeleted(ILogger logger, bool useLocalStorage);
}
