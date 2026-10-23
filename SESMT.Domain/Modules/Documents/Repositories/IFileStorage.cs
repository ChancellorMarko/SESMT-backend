namespace SESMT.Application.Common.Interfaces;

public sealed record FileStorageResult(
    string Provider,
    string Bucket,
    string ObjectKey,
    string FileHash,
    long SizeBytes);

/// <summary>
/// Abstração para armazenamento de arquivos físicos.
/// MVP: implementação local (disco).
/// Fase 2+: S3/MinIO/Azure.
/// </summary>
public interface IFileStorage
{
    Task<FileStorageResult> UploadAsync(
        Stream content,
        string fileName,
        string mimeType,
        CancellationToken ct = default);

    Task<Stream> DownloadAsync(string objectKey, CancellationToken ct = default);

    Task DeleteAsync(string objectKey, CancellationToken ct = default);
}
