using System.Text;

namespace ZipSplitTool;

public class ZipSplitHelper
{
    private const int MinSharedFileSize = 64 * 1024;

    private ISharedDataChunkStorage chunkStorage;

    public ZipSplitHelper(ISharedDataChunkStorage chunkStorage)
    {
        this.chunkStorage = chunkStorage;
    }

    public void ProcessZip(string path, IReceiptFileWriter writer)
    {
        using var stream = File.OpenRead(path);
        var archive = new ZipArchive();
        archive.ReadHeaders(stream);

        var localHeaderOffsets = new List<long>();
        foreach (var entry in archive.ReadFileHeaders(stream))
        {
            if (entry.CompressedSize < MinSharedFileSize)
                continue;
            localHeaderOffsets.Add((long)entry.LocalFileHeaderOffset);
        }

        localHeaderOffsets.Sort();

        stream.Seek(0, SeekOrigin.Begin);
        var fileHash = StreamHelper.HashData(stream, stream.Length);
        writer.WriteHeader(fileHash, stream.Length);

        stream.Seek(0, SeekOrigin.Begin);
        foreach (var offset in localHeaderOffsets)
        {
            // Console.WriteLine(stream.Position + "->" + offset + "(" + (offset-stream.Position) + ")");
            if (stream.Position > offset)
                throw new Exception("jumped over some data, the output file would be incorrect");
            if (stream.Position < offset)
                CopyRawData(stream, writer, offset - stream.Position, true);

            var localHeaderData = new byte[ZipFormat.LocalFileHeader.MinSize];
            if (stream.Read(localHeaderData) != localHeaderData.Length)
                throw new Exception("failed to read localHeaderData");
            var localHeader = ZipFormat.ParseLocalFileHeader(localHeaderData, 0, localHeaderData.Length);

            var fileName = new byte[localHeader.FileNameLength];
            if (stream.Read(fileName) != fileName.Length)
                throw new Exception("failed to read file name from local header");

            var remainingHeaderDataSize = localHeader.Size - localHeaderData.Length - fileName.Length;

            writer.StartDataChunk(localHeaderData.Length + fileName.Length + remainingHeaderDataSize);
            writer.WriteDataChunkContent(localHeaderData);
            writer.WriteDataChunkContent(fileName);
            CopyRawData(stream, writer, remainingHeaderDataSize, false);

            var dataStartPos = stream.Position;
            var dataHash = StreamHelper.HashData(stream, (long)localHeader.CompressedSize);
            stream.Seek(dataStartPos, SeekOrigin.Begin);
            chunkStorage.AddChunk(dataHash, stream, (long)localHeader.CompressedSize, path,
                Encoding.UTF8.GetString(fileName));
            stream.Seek(dataStartPos + (long)localHeader.CompressedSize, SeekOrigin.Begin);

            writer.WriteSharedChunk(dataHash);
        }

        CopyRawData(stream, writer, stream.Length - stream.Position, true);
    }

    public byte[] RestoreOriginalFile(IReceiptFileReader reader, Stream output, bool useSetLength)
    {
        var header = reader.ReadHeader();
        if (useSetLength)
            output.SetLength(header.FileSize);

        while (!reader.IsEof())
        {
            var chunk = reader.ReadChunkHeader();
            if (chunk is IReceiptFileReader.DataChunk)
            {
                StreamHelper.CopyData(reader.BaseStream, output, ((IReceiptFileReader.DataChunk)chunk).Size);
            }
            else if (chunk is IReceiptFileReader.SharedChunk)
            {
                using var fileStream = chunkStorage.OpenChunk(((IReceiptFileReader.SharedChunk)chunk).Hash);
                fileStream.CopyTo(output);
            }
        }

        return header.FileHash;
    }

    private static void CopyRawData(Stream from, IReceiptFileWriter to, long size, bool writeHeader)
    {
        if (writeHeader)
            to.StartDataChunk(size);

        if (to is BinaryReceiptFileEstimator)
        {
            ((BinaryReceiptFileEstimator)to).WriteDataChunkContent(size);
            from.Seek(size, SeekOrigin.Current);
            return;
        }

        Span<byte> buf = stackalloc byte[4 * 1024 * 1024];
        while (size > 0)
        {
            var read = from.Read(buf[..(int)Math.Min(size, buf.Length)]);
            to.WriteDataChunkContent(buf[..read]);
            size -= read;
        }
    }
}