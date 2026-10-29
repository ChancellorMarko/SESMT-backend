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
    private readonly string _rootPrefix;
    private readonly StringComparison _comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public LocalFileStorage(IOptions<LocalStorageOptions> options)
    {
        _rootPath = Path.GetFullPath(options.Value.RootPath);
        _rootPrefix = Path.TrimEndingDirectorySeparator(_rootPath) + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(_rootPath);
        EnsureNoLinks(_rootPath);
    }

    private string Resolve(string objectKey)
    {
        if (string.IsNullOrWhiteSpace(objectKey) || Path.IsPathRooted(objectKey) || objectKey.Contains('\\') || objectKey.Split('/').Any(x => x is "." or ".." or ""))
            throw new UnauthorizedAccessException("Chave de armazenamento inválida.");
        var resolved = Path.GetFullPath(Path.Combine(_rootPath, objectKey.Replace('/', Path.DirectorySeparatorChar)));
        if (!resolved.StartsWith(_rootPrefix, _comparison)) throw new UnauthorizedAccessException("Arquivo fora do armazenamento autorizado.");
        EnsureNoLinks(resolved);
        return resolved;
    }

    private void EnsureNoLinks(string path)
    {
        var current = path;
        while (current is not null && (current.Equals(_rootPath, _comparison) || current.StartsWith(_rootPrefix, _comparison)))
        {
            if ((File.Exists(current) || Directory.Exists(current)) && File.GetAttributes(current).HasFlag(FileAttributes.ReparsePoint))
                throw new UnauthorizedAccessException("Links de sistema de arquivos não são permitidos no armazenamento.");
            if (current.Equals(_rootPath, _comparison)) break;
            current = Path.GetDirectoryName(current);
        }
    }

    public async Task<FileStorageResult> UploadAsync(Stream content, string fileName, string mimeType, CancellationToken ct = default)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension is not (".pdf" or ".png" or ".jpg" or ".jpeg")) throw new ArgumentException("Extensão não permitida.");
        var now = DateTimeOffset.UtcNow;
        var objectKey = $"{now.Year}/{now.Month:00}/{Guid.NewGuid():N}{extension}";
        var target = Resolve(objectKey);
        var temporary = Resolve(objectKey + ".part");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        EnsureNoLinks(target);
        try
        {
            long length = 0;
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
            {
                var buffer = new byte[81920];
                int count;
                while ((count = await content.ReadAsync(buffer, ct)) > 0)
                {
                    hash.AppendData(buffer.AsSpan(0, count));
                    await output.WriteAsync(buffer.AsMemory(0, count), ct);
                    length += count;
                }
                await output.FlushAsync(ct);
            }
            ct.ThrowIfCancellationRequested();
            File.Move(temporary, target);
            return new("LOCAL", "default", objectKey, Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(), length);
        }
        catch
        {
            if (File.Exists(temporary)) File.Delete(temporary);
            throw;
        }
    }

    public Task<Stream> DownloadAsync(string objectKey, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var path = Resolve(objectKey);
        if (!File.Exists(path)) throw new FileNotFoundException("Arquivo não disponível.");
        return Task.FromResult<Stream>(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous));
    }

    public Task DeleteAsync(string objectKey, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var path = Resolve(objectKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }
}
