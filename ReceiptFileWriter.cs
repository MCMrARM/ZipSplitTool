namespace ZipSplitTool;

public interface IReceiptFileWriter
{
    void WriteHeader(byte[] fileHash, long fileSize);
    void WriteSharedChunk(byte[] hash);
    void StartDataChunk(long size);
    void WriteDataChunkContent(Span<byte> data);
    void WriteDataChunkContent(byte[] data, int offset, int length);
}

public class BinaryReceiptFileWriter : IReceiptFileWriter
{
    private BinaryWriter writer;
    
    public BinaryReceiptFileWriter(BinaryWriter writer)
    {
        this.writer = writer;
    }

    public void WriteHeader(byte[] fileHash, long fileSize)
    {
        if (fileHash.Length > byte.MaxValue)
            throw new Exception("file hash too long");
        writer.Write((byte) 'Z');
        writer.Write((byte) 'i');
        writer.Write((byte) 'p');
        writer.Write((byte) 'S');
        writer.Write((byte) 0x01); // Version 
        writer.Write((byte) fileHash.Length);
        writer.Write(fileHash);
        writer.Write(fileSize);
    }
    
    public void WriteSharedChunk(byte[] hash)
    {
        if (hash.Length > byte.MaxValue)
            throw new Exception("hash too long");
        writer.Write((byte) 0x01); // Chunk type: Shared
        writer.Write((byte) hash.Length);
        writer.Write(hash);
    }

    public void StartDataChunk(long size)
    {
        if (size > ushort.MaxValue)
        {
            writer.Write((byte) 0x03); // Chunk type: Data (8 byte length)
            writer.Write((ulong) size);
        }
        else
        {
            writer.Write((byte) 0x02); // Chunk type: Data (2 byte length)
            writer.Write((ushort) size);
        }
    }

    public void WriteDataChunkContent(Span<byte> data)
    {
        writer.Write(data);
    }

    public void WriteDataChunkContent(byte[] data, int offset, int length)
    {
        writer.Write(data, offset, length);
    }
}

public class BinaryReceiptFileEstimator : IReceiptFileWriter
{
    public long Size = 0;

    public void WriteHeader(byte[] fileHash, long fileSize)
    {
        Size += 4 + 1 + 1 + fileHash.Length + 8;
    }

    public void WriteSharedChunk(byte[] hash)
    {
        if (hash.Length > byte.MaxValue)
            throw new Exception("hash too long");
        Size += 2 + hash.Length;
    }

    public void StartDataChunk(long size)
    {
        if (size > ushort.MaxValue)
            Size += 1 + 8;
        else
            Size += 1 + 2;
    }

    public void WriteDataChunkContent(Span<byte> data)
    {
        Size += data.Length;
    }
    
    public void WriteDataChunkContent(byte[] data, int offset, int length)
    {
        Size += length;
    }

    public void WriteDataChunkContent(long length)
    {
        Size += length;
    }
}