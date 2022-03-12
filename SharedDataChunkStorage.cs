using System.Text.Json;

namespace ZipSplitTool;

public interface ISharedDataChunkStorage
{
    Stream OpenChunk(byte[] hash);
    void AddChunk(byte[] hash, Stream stream, long length, string containerFileName, string fileName);
    void Flush();
}

public class SharedDataChunkStorageEstimator : ISharedDataChunkStorage
{
    private HashSet<string> chunkSet = new();
    public long TotalSize;
    public int Count;

    public Stream OpenChunk(byte[] hash)
    {
        throw new NotImplementedException();
    }

    public void AddChunk(byte[] hash, Stream stream, long length, string containerFileName, string fileName)
    {
        if (chunkSet.Contains(Convert.ToBase64String(hash)))
            return;
        chunkSet.Add(Convert.ToBase64String(hash));
        TotalSize += length;
        Count++;
    }

    public void Flush()
    {
    }
}

public class DiskDataChunkStorage : ISharedDataChunkStorage, IDisposable
{
    private static readonly JsonSerializerOptions jsonSerializerOptions = new()
    {
        IncludeFields = true,
    };

    private class IndexFileData
    {
        public long Size;
        public HashSet<Tuple<string, string>> Refs = new();
    }

    private class IndexData
    {
        public Dictionary<string, IndexFileData> Files = new();
    }

    private bool readOnly;
    private FileStream? lockFile;
    private string chunkDir;
    private Dictionary<string, IndexData> indexFiles = new();
    private HashSet<string> dirtyIndexFiles = new();
    public long AddedTotalSize;
    public int AddedCount;

    public DiskDataChunkStorage(string chunkDir, bool readOnly)
    {
        this.readOnly = readOnly;
        this.chunkDir = chunkDir;
        if (!readOnly)
            lockFile = new FileStream(Path.Combine(chunkDir, "Lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
    }

    private string GetBucketPath(byte[] hash)
    {
        return Path.Combine(chunkDir, Convert.ToHexString(hash, 0, 1));
    }

    private IndexData OpenIndexForHash(byte[] hash)
    {
        var bucketPath = GetBucketPath(hash);
        if (!indexFiles.ContainsKey(bucketPath))
        {
            if (Directory.Exists(bucketPath) && File.Exists(Path.Combine(bucketPath, "Index.json")))
            {
                var txt = File.ReadAllText(Path.Combine(bucketPath, "Index.json"));
                indexFiles[bucketPath] = JsonSerializer.Deserialize<IndexData>(txt, jsonSerializerOptions)!;
            }
            else
            {
                indexFiles[bucketPath] = new IndexData();
            }
        }

        return indexFiles[bucketPath];
    }

    private void SaveIndexImmediately(string bucketPath, IndexData data)
    {
        if (!Directory.Exists(bucketPath))
            Directory.CreateDirectory(bucketPath);

        var tmpFile = Path.Combine(bucketPath, "Index.json.tmp");
        var txt = JsonSerializer.Serialize(data, jsonSerializerOptions);
        File.WriteAllText(tmpFile, txt);
        File.Move(tmpFile, Path.Combine(bucketPath, "Index.json"), true);
    }

    public Stream OpenChunk(byte[] hash)
    {
        var hashString = Convert.ToHexString(hash);
        var bucketPath = GetBucketPath(hash);
        var chunkPath = Path.Combine(bucketPath, hashString);
        return File.OpenRead(chunkPath);
    }

    public void AddChunk(byte[] hash, Stream stream, long length, string containerFileName, string fileName)
    {
        if (readOnly)
            throw new Exception("opened the chunk storage read-only");
        
        var bucketPath = GetBucketPath(hash);
        var index = OpenIndexForHash(hash);
        var hashString = Convert.ToHexString(hash);

        if (!index.Files.ContainsKey(hashString))
        {
            if (!Directory.Exists(bucketPath))
                Directory.CreateDirectory(bucketPath);

            var outPath = Path.Combine(bucketPath, hashString);
            using var outStream = File.OpenWrite(outPath);
            StreamHelper.CopyData(stream, outStream, length);

            index.Files[hashString] = new IndexFileData { Size = length };
            AddedTotalSize += length;
            AddedCount++;
        }

        index.Files[hashString].Refs.Add(new Tuple<string, string>(containerFileName, fileName));
        dirtyIndexFiles.Add(bucketPath);
    }

    public void Flush()
    {
        foreach (var bucketPath in dirtyIndexFiles)
        {
            SaveIndexImmediately(bucketPath, indexFiles[bucketPath]);
        }

        dirtyIndexFiles.Clear();
    }

    public void Dispose()
    {
        lockFile?.Dispose();
    }
}