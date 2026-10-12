using SESMT.Domain.Common.AggregateRoot;

namespace SESMT.Domain.Modules.Documents.Entities;

public class StoredFile : AggregateRootBase
{
    public string StorageProvider { get; private set; } = null!;
    public string Bucket { get; private set; } = null!;
    public string ObjectKey { get; private set; } = null!;
    public string FileName { get; private set; } = null!;
    public string MimeType { get; private set; } = null!;
    public long Size { get; private set; }
    public string FileHash { get; private set; } = null!;
    public bool IsEncrypted { get; private set; }
    public string AntivirusScanResult { get; private set; } = null!;

    protected StoredFile() { }

    public StoredFile(string storageProvider, string bucket, string objectKey, string fileName, string mimeType, long size, string fileHash, bool isEncrypted, string antivirusScanResult)
    {
        StorageProvider = storageProvider;
        Bucket = bucket;
        ObjectKey = objectKey;
        FileName = fileName;
        MimeType = mimeType;
        Size = size;
        FileHash = fileHash;
        IsEncrypted = isEncrypted;
        AntivirusScanResult = antivirusScanResult;
    }

    public void UpdateFileDetails(string storageProvider, string bucket, string objectKey, string fileName, string mimeType, long size, string fileHash, bool isEncrypted, string antivirusScanResult)
    {
        StorageProvider = storageProvider;
        Bucket = bucket;
        ObjectKey = objectKey;
        FileName = fileName;
        MimeType = mimeType;
        Size = size;
        FileHash = fileHash;
        IsEncrypted = isEncrypted;
        AntivirusScanResult = antivirusScanResult;
        
        MarkAsUpdated();
    }
}