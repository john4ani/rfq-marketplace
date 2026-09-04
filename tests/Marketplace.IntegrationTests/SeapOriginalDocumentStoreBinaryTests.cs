using System.Security.Cryptography;
using System.Text;
using Marketplace.Application.Seap;
using Marketplace.Infrastructure.Seap;
using Marketplace.Infrastructure.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Marketplace.IntegrationTests;

// Stage 1 guarantee: the Direct Hit dossier store must accept ANY byte stream — a signed .pdf.p7s, a
// .rar.p7s envelope or a DUAE .xml are persisted byte-identical and reopened without transformation. They
// are never „download-failed" just because they are not yet analyzable (analysis/preparation is a later,
// separate status). This test is deliberately DB-free so it runs without a Postgres container.
public sealed class SeapOriginalDocumentStoreBinaryTests
{
    [Fact]
    public async Task SignedEnvelopeNamesAreStoredByteIdenticalAndReopenedUntouched()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "seap-dossier-binary-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            IOptions<LocalStorageOptions> localStorage = Options.Create(new LocalStorageOptions
            {
                UseLocalStorage = true,
                LocalPath = tempRoot
            });
            LocalFileStorageService objectStorage = new(localStorage, NullLogger<LocalFileStorageService>.Instance);
            SeapOriginalDocumentStore dossier = new(
                objectStorage,
                Options.Create(new SeapStorageOptions { Enabled = false }),
                localStorage,
                Options.Create(new SeapOptions
                {
                    MaxOriginalDocumentBytes = 64L * 1024 * 1024,
                    MaxTenderOriginalDocumentsBytes = 256L * 1024 * 1024
                }),
                NullLogger<SeapOriginalDocumentStore>.Instance);

            Guid tenderId = Guid.NewGuid();
            (string FileName, byte[] Bytes)[] documents =
            [
                // Signed PAdES/CAdES envelope: arbitrary binary that is NOT a readable PDF — must still store.
                ("Rezistenta.pdf.p7s", BinaryPayload(4096, seed: 1)),
                // A .rar wrapped in a signature container — same guarantee.
                ("Proiect tehnic.rar.p7s", BinaryPayload(8192, seed: 2)),
                // DUAE form XML — stored verbatim, never parsed at Stage 1.
                ("DUAE_CERERE_1.xml", Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><DUAE><ESPD>date</ESPD></DUAE>"))
            ];

            foreach ((string fileName, byte[] bytes) in documents)
            {
                StoredSeapOriginalDocument stored;
                await using (MemoryStream content = new(bytes))
                {
                    stored = await dossier.StoreOriginalAsync(tenderId, fileName, content, CancellationToken.None);
                }

                Assert.StartsWith($"local://seap-originals/{tenderId}", stored.Reference, StringComparison.Ordinal);
                Assert.Equal(bytes.Length, stored.SizeBytes);
                Assert.Equal(
                    Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
                    stored.ContentSha256);

                // Reopen and compare byte-for-byte: the dossier never transformed the payload.
                StorageObject? reopened = await objectStorage.OpenReadAsync(stored.Reference, CancellationToken.None);
                Assert.NotNull(reopened);
                await using MemoryStream copy = new();
                await reopened!.Content.CopyToAsync(copy);
                Assert.Equal(bytes, copy.ToArray());
            }
        }
        finally
        {
            try { Directory.Delete(tempRoot, recursive: true); }
            catch { /* best-effort cleanup */ }
        }
    }

    private static byte[] BinaryPayload(int length, int seed)
    {
        byte[] payload = new byte[length];
        for (int i = 0; i < length; i++)
            payload[i] = (byte)((i * 31 + seed * 7) % 251); // deterministic, arbitrary, non-text bytes
        return payload;
    }
}
