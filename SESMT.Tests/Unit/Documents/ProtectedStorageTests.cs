using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SESMT.Infrastructure.Storage;

namespace SESMT.Tests.Unit.Documents;

public sealed class ProtectedStorageTests
{
    [Theory]
    [InlineData("../outside.pdf")]
    [InlineData("/etc/passwd")]
    [InlineData("folder/../../outside.pdf")]
    [InlineData(@"folder\..\outside.pdf")]
    [InlineData("folder//outside.pdf")]
    [InlineData("")]
    public async Task ObjectKeyCannotEscapeConfiguredRoot(string key)
    {
        using var fixture = new StorageFixture();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Storage.DownloadAsync(key));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Storage.DeleteAsync(key));
        Assert.Equal("sentinel", await File.ReadAllTextAsync(fixture.Sentinel));
    }

    [Fact]
    public async Task StoredObject_IsRandomAndMatchesHashSizeAndContent()
    {
        using var fixture = new StorageFixture();
        var bytes = Encoding.ASCII.GetBytes("%PDF-1.7\nfictitious test");
        var result = await fixture.Storage.UploadAsync(new MemoryStream(bytes), "../../untrusted-name.pdf", "application/pdf");
        Assert.DoesNotContain("untrusted", result.ObjectKey);
        Assert.Equal(bytes.LongLength, result.SizeBytes);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), result.FileHash);
        await using var download = await fixture.Storage.DownloadAsync(result.ObjectKey);
        using var output = new MemoryStream(); await download.CopyToAsync(output);
        Assert.Equal(bytes, output.ToArray());
        await download.DisposeAsync();
        await fixture.Storage.DeleteAsync(result.ObjectKey);
        Assert.Empty(Directory.EnumerateFiles(fixture.Root, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task InterruptedUpload_DoesNotLeaveAvailableObjectOrPartFile()
    {
        using var fixture = new StorageFixture();
        await Assert.ThrowsAsync<IOException>(() => fixture.Storage.UploadAsync(new InterruptedStream(), "evidence.pdf", "application/pdf"));
        Assert.Empty(Directory.EnumerateFiles(fixture.Root, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Cancellation_DoesNotLeaveAnObject()
    {
        using var fixture = new StorageFixture();
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Storage.UploadAsync(new MemoryStream(new byte[10]),
            "evidence.pdf", "application/pdf", cancellation.Token));
        Assert.Empty(Directory.EnumerateFiles(fixture.Root, "*", SearchOption.AllDirectories));
    }

    private sealed class InterruptedStream : MemoryStream
    {
        private bool sent;
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (sent) throw new IOException("Simulated interruption");
            sent = true; buffer.Span[0] = 1;
            return ValueTask.FromResult(1);
        }
    }

    private sealed class StorageFixture : IDisposable
    {
        private readonly string container;
        private readonly string allowedBase;
        public string Root { get; }
        public string Sentinel { get; }
        public LocalFileStorage Storage { get; }
        public StorageFixture()
        {
            allowedBase = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "sesmt-protected-storage-tests"));
            container = Path.GetFullPath(Path.Combine(allowedBase, Guid.NewGuid().ToString("N")));
            Root = Path.Combine(container, "storage");
            Directory.CreateDirectory(container);
            Sentinel = Path.Combine(container, "outside.pdf");
            File.WriteAllText(Sentinel, "sentinel");
            Storage = new(Options.Create(new LocalStorageOptions { RootPath = Root }));
        }
        public void Dispose()
        {
            // Only this generated test container may be recursively removed.
            var resolved = Path.GetFullPath(container);
            var prefix = Path.TrimEndingDirectorySeparator(allowedBase) + Path.DirectorySeparatorChar;
            if (!resolved.StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
                || Path.GetFileName(resolved).Length != 32) throw new InvalidOperationException("Unsafe test cleanup target.");
            if (Directory.Exists(resolved)) Directory.Delete(resolved, recursive: true);
        }
    }
}
