using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Marketplace.Application.Seap;
using Marketplace.Domain.Seap;
using Marketplace.Infrastructure.Seap;
using Marketplace.Infrastructure.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace Marketplace.IntegrationTests;

// SeapAttachmentTextReader: best-effort text extraction from the published attachments so the on-demand
// „Sinteză AI" generation can be grounded in the real Fișa de date / Caiet de sarcini content. Unreadable
// formats and failed downloads degrade to null — the summary always falls back to notice metadata.
public sealed class SeapAttachmentTextReaderTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static SeapAttachmentTextReader Reader(ISeapApiClient apiClient, IStorageService? storage = null) =>
        new(apiClient, storage ?? new EmptyStorageService(), NullLogger<SeapAttachmentTextReader>.Instance);

    private static SeapTenderAttachment Attachment(string fileName, string reference) =>
        SeapTenderAttachment.Create(Guid.NewGuid(), fileName, reference, Now);

    [Fact]
    public async Task ReadsPlainTextAttachmentAndLabelsItWithTheFileName()
    {
        SeapAttachmentTextReader reader = Reader(new StreamApiClient("Garanție de participare: 120.000 RON\nTermen de execuție: 12 luni."));

        SeapAttachmentReadResult read = await reader.ReadPublishedTextAsync(
            [Attachment("fisa-date.txt", "https://e-licitatie.ro/api-pub/files/fisa-date.txt")], CancellationToken.None);
        string? text = read.Text;

        Assert.NotNull(text);
        Assert.Contains("fisa-date.txt", text);
        Assert.Contains("Garanție de participare: 120.000 RON", text);
        Assert.Contains("Termen de execuție: 12 luni", text);
        // Content digest of the real downloaded bytes is returned alongside the text.
        SeapAttachmentContentDigest digest = Assert.Single(read.Digests);
        Assert.Equal("fisa-date.txt", digest.FileName);
        Assert.Equal(64, digest.ContentSha256.Length);
        Assert.True(digest.SizeBytes > 0);
    }

    [Fact]
    public async Task ReadsTextAttachmentsFromInsideAZipArchive()
    {
        using var archiveStream = new MemoryStream();
        using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            ZipArchiveEntry entry = archive.CreateEntry("Caiet de sarcini.txt");
            await using Stream entryStream = entry.Open();
            await entryStream.WriteAsync("Criteriul de atribuire: prețul cel mai scăzut"u8.ToArray());
        }
        archiveStream.Position = 0;
        SeapAttachmentTextReader reader = Reader(new EmptyApiClient(), new StreamStorageService(archiveStream));

        SeapAttachmentReadResult read = await reader.ReadPublishedTextAsync(
            [Attachment("caiet.zip", "local://seap/tender/caiet.zip")], CancellationToken.None);
        string? text = read.Text;

        Assert.NotNull(text);
        Assert.Contains("Caiet de sarcini.txt", text);
        Assert.Contains("prețul cel mai scăzut", text);
    }

    [Fact]
    public async Task ReturnsNullWhenNoAttachmentYieldsReadableText()
    {
        SeapAttachmentTextReader reader = new(
            new StreamApiClient("remote-file-bytes"), new EmptyStorageService(),
            NullLogger<SeapAttachmentTextReader>.Instance);

        SeapAttachmentReadResult read = await reader.ReadPublishedTextAsync(
            [Attachment("fisa-date.pdf", "https://e-licitatie.ro/api-pub/files/fisa-date.pdf")], CancellationToken.None);
        string? text = read.Text;

        Assert.Null(text);
    }

    [Fact]
    public async Task ToleratesFailedDownloadsAndSkipsTheAttachment()
    {
        SeapAttachmentTextReader reader = new(
            new ThrowingApiClient(), new EmptyStorageService(), NullLogger<SeapAttachmentTextReader>.Instance);

        SeapAttachmentReadResult read = await reader.ReadPublishedTextAsync(
            [Attachment("fisa-date.txt", "https://e-licitatie.ro/api-pub/files/fisa-date.txt")], CancellationToken.None);
        string? text = read.Text;

        Assert.Null(text);
    }

    [Fact]
    public async Task ReadsTextFromAP7sCmsSignedWrapper()
    {
        byte[] inner = Encoding.UTF8.GetBytes("Garanție de participare: 120.000 RON\nTermen de execuție: 12 luni.");
        byte[] p7s = CmsSign(inner);
        SeapAttachmentTextReader reader = new(
            new BytesApiClient(p7s), new EmptyStorageService(), NullLogger<SeapAttachmentTextReader>.Instance);

        SeapAttachmentReadResult read = await reader.ReadPublishedTextAsync(
            [Attachment("fisa-date.pdf.p7s", "https://e-licitatie.ro/api-pub/files/fisa-date.pdf.p7s")], CancellationToken.None);

        Assert.NotNull(read.Text);
        Assert.Contains("Garanție de participare: 120.000 RON", read.Text);
        // The content digest is computed over the ORIGINAL signed bytes, not the unwrapped payload.
        Assert.Single(read.Digests);
    }

    private static byte[] CmsSign(byte[] content)
    {
        using RSA rsa = RSA.Create(2048);
        CertificateRequest request = new("CN=SEAP Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var signed = new SignedCms(new ContentInfo(content), detached: false);
        signed.ComputeSignature(new CmsSigner(certificate));
        return signed.Encode();
    }
}

public sealed class BytesApiClient(byte[] content) : ISeapApiClient
{
    public Task<IReadOnlyCollection<SeapTenderNoticeDto>> FetchRecentNoticesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<SeapTenderNoticeDto>>([]);

    public Task<IReadOnlyCollection<SeapTenderNoticeDto>> FetchNoticePageAsync(
        int pageIndex, int pageSize, DateOnly? startPublicationDate, DateOnly? endPublicationDate,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<SeapTenderNoticeDto>>([]);

    public Task<SeapTenderNoticeDto?> FetchNoticeByNumberAsync(string noticeNo, CancellationToken cancellationToken) =>
        Task.FromResult<SeapTenderNoticeDto?>(null);

    public Task<Stream> DownloadAttachmentAsync(string downloadUrl, CancellationToken cancellationToken) =>
        Task.FromResult<Stream>(new MemoryStream(content));

    public Task<IReadOnlyCollection<SeapNoticeDocumentDto>> FetchNoticeDocumentsAsync(
        int noticeId, int sysNoticeTypeId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<SeapNoticeDocumentDto>>([]);
}

public sealed class StreamApiClient(string text) : ISeapApiClient
{
    public Task<IReadOnlyCollection<SeapTenderNoticeDto>> FetchRecentNoticesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<SeapTenderNoticeDto>>([]);

    public Task<IReadOnlyCollection<SeapTenderNoticeDto>> FetchNoticePageAsync(
        int pageIndex, int pageSize, DateOnly? startPublicationDate, DateOnly? endPublicationDate,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<SeapTenderNoticeDto>>([]);

    public Task<SeapTenderNoticeDto?> FetchNoticeByNumberAsync(string noticeNo, CancellationToken cancellationToken) =>
        Task.FromResult<SeapTenderNoticeDto?>(null);

    public Task<Stream> DownloadAttachmentAsync(string downloadUrl, CancellationToken cancellationToken) =>
        Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(text)));

    public Task<IReadOnlyCollection<SeapNoticeDocumentDto>> FetchNoticeDocumentsAsync(
        int noticeId, int sysNoticeTypeId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<SeapNoticeDocumentDto>>([]);
}

public sealed class ThrowingApiClient : ISeapApiClient
{
    public Task<IReadOnlyCollection<SeapTenderNoticeDto>> FetchRecentNoticesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<SeapTenderNoticeDto>>([]);

    public Task<IReadOnlyCollection<SeapTenderNoticeDto>> FetchNoticePageAsync(
        int pageIndex, int pageSize, DateOnly? startPublicationDate, DateOnly? endPublicationDate,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<SeapTenderNoticeDto>>([]);

    public Task<SeapTenderNoticeDto?> FetchNoticeByNumberAsync(string noticeNo, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("SEAP indisponibil.");

    public Task<Stream> DownloadAttachmentAsync(string downloadUrl, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("SEAP indisponibil.");

    public Task<IReadOnlyCollection<SeapNoticeDocumentDto>> FetchNoticeDocumentsAsync(
        int noticeId, int sysNoticeTypeId, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("SEAP indisponibil.");
}

public sealed class EmptyApiClient : ISeapApiClient
{
    public Task<IReadOnlyCollection<SeapTenderNoticeDto>> FetchRecentNoticesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<SeapTenderNoticeDto>>([]);

    public Task<IReadOnlyCollection<SeapTenderNoticeDto>> FetchNoticePageAsync(
        int pageIndex, int pageSize, DateOnly? startPublicationDate, DateOnly? endPublicationDate,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<SeapTenderNoticeDto>>([]);

    public Task<SeapTenderNoticeDto?> FetchNoticeByNumberAsync(string noticeNo, CancellationToken cancellationToken) =>
        Task.FromResult<SeapTenderNoticeDto?>(null);

    public Task<Stream> DownloadAttachmentAsync(string downloadUrl, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<IReadOnlyCollection<SeapNoticeDocumentDto>> FetchNoticeDocumentsAsync(
        int noticeId, int sysNoticeTypeId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<SeapNoticeDocumentDto>>([]);
}

public sealed class StreamStorageService(Stream content) : IStorageService
{
    public Task<string> UploadAsync(
        string bucket, string key, string fileName, Stream content, string contentType,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<StorageObject?> OpenReadAsync(string reference, CancellationToken cancellationToken)
    {
        content.Position = 0;
        return Task.FromResult<StorageObject?>(new StorageObject(content, "application/octet-stream"));
    }
}

public sealed class EmptyStorageService : IStorageService
{
    public Task<string> UploadAsync(
        string bucket, string key, string fileName, Stream content, string contentType,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<StorageObject?> OpenReadAsync(string reference, CancellationToken cancellationToken) =>
        Task.FromResult<StorageObject?>(null);
}

