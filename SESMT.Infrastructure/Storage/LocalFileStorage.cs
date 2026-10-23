using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using SESMT.Application.Common.Interfaces;

namespace SESMT.Infrastructure.Storage;

public sealed class LocalStorageOptions
{
    public const string SectionName = "Storage:Local";
    public string RootPath { get; set; } = "storage";
}

public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _rootPath;

    public LocalFileStorage(IOptions<LocalStorageOptions> options)
    {
        _rootPath = Path.GetFullPath(options.Value.RootPath);
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<FileStorageResult> UploadAsync(
        Stream content, string fileName, string mimeType, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();

        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        // Organiza por ano/mês para não virar um diretório gigante
        var now = DateTimeOffset.UtcNow;
        var relativeKey = Path.Combine(
            now.Year.ToString(),
            now.Month.ToString("00"),
            $"{Guid.NewGuid():N}{Path.GetExtension(fileName)}");

        var fullPath = Path.Combine(_rootPath, relativeKey);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllBytesAsync(fullPath, bytes, ct);

        return new FileStorageResult(
            Provider: "LOCAL",
            Bucket: "default",
            ObjectKey: relativeKey.Replace('\\', '/'),
            FileHash: hash,
            SizeBytes: bytes.LongLength);
    }

    public Task<Stream> DownloadAsync(string objectKey, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_rootPath, objectKey);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Arquivo não encontrado: {objectKey}");

        return Task.FromResult<Stream>(File.OpenRead(fullPath));
    }

    public Task DeleteAsync(string objectKey, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_rootPath, objectKey);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
        return Task.CompletedTask;
    }
}
