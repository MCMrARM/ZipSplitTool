namespace ZipSplitTool;

public interface IReceiptFileReader
{
    public class Header
    {
        public byte[] FileHash = Array.Empty<byte>();
        public long FileSize;
    }

    public class Chunk
    {
    }

    public class DataChunk : Chunk
    {
        public long Size;
    }

    public class SharedChunk : Chunk
    {
        public byte[] Hash = Array.Empty<byte>();
    }

    Stream BaseStream { get; }

    Header ReadHeader();
    Chunk ReadChunkHeader();
    bool IsEof();
}

public class BinaryReceiptFileReader : IReceiptFileReader
{
    private BinaryReader reader;

    public Stream BaseStream
    {
        get => reader.BaseStream;
    }

    public BinaryReceiptFileReader(BinaryReader reader)
    {
        this.reader = reader;
    }

    public IReceiptFileReader.Header ReadHeader()
    {
        byte[] sig = reader.ReadBytes(5);
        if (sig[0] != 'Z' || sig[1] != 'i' || sig[2] != 'p' || sig[3] != 'S')
            throw new Exception("bad sig");
        if (sig[4] != 1)
            throw new Exception("bad version");

        var hdr = new IReceiptFileReader.Header();
        hdr.FileHash = reader.ReadBytes(reader.ReadByte());
        hdr.FileSize = reader.ReadInt64();
        return hdr;
    }

    public IReceiptFileReader.Chunk ReadChunkHeader()
    {
        var chunkType = reader.ReadByte();

        switch (chunkType)
        {
            case 1:
            {
                var chunk = new IReceiptFileReader.SharedChunk();
                chunk.Hash = reader.ReadBytes(reader.ReadByte());
                return chunk;
            }
            case 2:
            {
                var chunk = new IReceiptFileReader.DataChunk();
                chunk.Size = reader.ReadUInt16();
                return chunk;
            }
            case 3:
            {
                var chunk = new IReceiptFileReader.DataChunk();
                chunk.Size = (long)reader.ReadUInt64();
                return chunk;
            }
            default:
                throw new Exception("unknown chunk type");
        }
    }

    public bool IsEof()
    {
        return reader.BaseStream.Position == reader.BaseStream.Length;
    }
}