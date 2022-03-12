namespace ZipSplitTool;

public class ZipFormat
{
    public static readonly byte[] SigEocd = { 0x50, 0x4b, 0x05, 0x06 };
    public static readonly byte[] SigEocd64 = { 0x50, 0x4b, 0x06, 0x06 };
    public static readonly byte[] SigEocd64Locator = { 0x50, 0x4b, 0x06, 0x07 };
    public static readonly byte[] SigCentralDirectoryFileHeader = { 0x50, 0x4b, 0x01, 0x02 };
    public static readonly byte[] SigLocalFileHeader = { 0x50, 0x4b, 0x03, 0x04 };


    public struct CentralDirectoryFileHeader
    {
        public int Size;

        public UInt16 VersionMadeBy;
        public UInt16 VersionNeededToExtract;
        public UInt16 Flags;
        public UInt16 CompressionMethod;
        public UInt16 LastModificationTime;
        public UInt16 LastModificationDate;
        public UInt32 DataCrc32;
        public UInt64 CompressedSize;
        public UInt64 UncompressedSize;
        public UInt16 FileNameLength;
        public UInt16 ExtraFieldLength;
        public UInt16 CommentFieldLength;
        public UInt32 FileStartDisk;
        public UInt16 InternalFileAttributes;
        public UInt32 ExternalFileAttributes;
        public UInt64 LocalFileHeaderOffset;

        public byte[] FileName;
    }
    
    public struct LocalFileHeader
    {
        public const int MinSize = 30;

        public int Size;

        public UInt16 VersionNeededToExtract;
        public UInt16 Flags;
        public UInt16 CompressionMethod;
        public UInt16 LastModificationTime;
        public UInt16 LastModificationDate;
        public UInt32 DataCrc32;
        public UInt64 CompressedSize;
        public UInt64 UncompressedSize;
        public UInt16 FileNameLength;
        public UInt16 ExtraFieldLength;
    }

    public struct Eocd
    {
        public int Size;

        public UInt16 NumberOfThisDisk;
        public UInt16 CentralDirectoryStartDisk;
        public UInt16 CentralDirectoryThisDiskRecords;
        public UInt16 CentralDirectoryTotalRecords;
        public UInt32 CentralDirectorySize;
        public UInt32 CentralDirectoryOffset;
        public UInt16 CommentLength;
        public byte[] Comment;

        public bool IsZip64 => CentralDirectoryOffset == 0xffffffff;
    }

    public struct Eocd64
    {
        public const int MinSizeForSizeRead = 12;

        public UInt64 Size;
        public UInt16 VersionMadeBy;
        public UInt16 VersionNeededToExtract;
        public UInt32 NumberOfThisDisk;
        public UInt32 CentralDirectoryStartDisk;
        public UInt64 CentralDirectoryThisDiskRecords;
        public UInt64 CentralDirectoryTotalRecords;
        public UInt64 CentralDirectorySize;
        public UInt64 CentralDirectoryOffset;
    }

    public struct Eocd64Locator
    {
        public const int Size = 20;

        public UInt32 EocdStartDisk;
        public UInt64 EocdOffset;
        public UInt32 NumberOfDisks;
    }

    public static Eocd ParseEocd(byte[] buf, int offset, int size, bool readComment = false)
    {
        using var reader = new BinaryReader(new MemoryStream(buf, offset, size));
        if (!reader.ReadBytes(4).SequenceEqual(SigEocd))
            throw new Exception("bad eocd64 locator signature");

        var retval = new Eocd();
        retval.Size = size;
        retval.NumberOfThisDisk = reader.ReadUInt16();
        retval.CentralDirectoryStartDisk = reader.ReadUInt16();
        retval.CentralDirectoryThisDiskRecords = reader.ReadUInt16();
        retval.CentralDirectoryTotalRecords = reader.ReadUInt16();
        retval.CentralDirectorySize = reader.ReadUInt32();
        retval.CentralDirectoryOffset = reader.ReadUInt32();
        retval.CommentLength = reader.ReadUInt16();
        if (readComment)
            retval.Comment = reader.ReadBytes(retval.CommentLength);
        return retval;
    }

    public static Eocd64Locator ParseEocd64Locator(byte[] buf, int offset, int size)
    {
        using var reader = new BinaryReader(new MemoryStream(buf, offset, size));
        if (!reader.ReadBytes(4).SequenceEqual(SigEocd64Locator))
            throw new Exception("bad eocd64 locator signature");

        var retval = new Eocd64Locator();
        retval.EocdStartDisk = reader.ReadUInt32();
        retval.EocdOffset = reader.ReadUInt64();
        retval.NumberOfDisks = reader.ReadUInt32();
        return retval;
    }

    public static long GetEocd64Size(byte[] buf, int offset, int size)
    {
        using var reader = new BinaryReader(new MemoryStream(buf, offset, size));
        if (!reader.ReadBytes(4).SequenceEqual(SigEocd64))
            throw new Exception("bad eocd64 locator signature");
        return (long)(reader.ReadUInt64() + 12);
    }

    public static Eocd64 ParseEocd64(byte[] buf, int offset, int size)
    {
        using var reader = new BinaryReader(new MemoryStream(buf, offset, size));
        if (!reader.ReadBytes(4).SequenceEqual(SigEocd64))
            throw new Exception("bad eocd64 signature");

        var retval = new Eocd64();
        retval.Size = reader.ReadUInt64() + 12;
        retval.VersionMadeBy = reader.ReadUInt16();
        retval.VersionNeededToExtract = reader.ReadUInt16();
        retval.NumberOfThisDisk = reader.ReadUInt32();
        retval.CentralDirectoryStartDisk = reader.ReadUInt32();
        retval.CentralDirectoryThisDiskRecords = reader.ReadUInt64();
        retval.CentralDirectoryTotalRecords = reader.ReadUInt64();
        retval.CentralDirectorySize = reader.ReadUInt64();
        retval.CentralDirectoryOffset = reader.ReadUInt64();
        return retval;
    }

    public static CentralDirectoryFileHeader ParseCentralDirectoryFileHeader(byte[] buf, int offset, int size)
    {
        using var reader = new BinaryReader(new MemoryStream(buf, offset, size));
        if (!reader.ReadBytes(4).SequenceEqual(SigCentralDirectoryFileHeader))
            throw new Exception("bad central directory file header signature");

        var retval = new CentralDirectoryFileHeader();
        retval.VersionMadeBy = reader.ReadUInt16();
        retval.VersionNeededToExtract = reader.ReadUInt16();
        retval.Flags = reader.ReadUInt16();
        retval.CompressionMethod = reader.ReadUInt16();
        retval.LastModificationTime = reader.ReadUInt16();
        retval.LastModificationDate = reader.ReadUInt16();
        retval.DataCrc32 = reader.ReadUInt32();
        retval.CompressedSize = reader.ReadUInt32();
        retval.UncompressedSize = reader.ReadUInt32();
        retval.FileNameLength = reader.ReadUInt16();
        retval.ExtraFieldLength = reader.ReadUInt16();
        retval.CommentFieldLength = reader.ReadUInt16();
        retval.FileStartDisk = reader.ReadUInt16();
        retval.InternalFileAttributes = reader.ReadUInt16();
        retval.ExternalFileAttributes = reader.ReadUInt32();
        retval.LocalFileHeaderOffset = reader.ReadUInt32();

        retval.FileName = reader.ReadBytes(retval.FileNameLength);

        for (int i = 0; i < retval.ExtraFieldLength - 3;)
        {
            var headerId = reader.ReadUInt16();
            var chunkSize = reader.ReadUInt16();
            var chunkData = reader.ReadBytes(chunkSize);
            var chunkMemStream = new MemoryStream(chunkData);
            using var chunkReader = new BinaryReader(chunkMemStream);

            if (headerId == 0x1)
            {
                if (chunkMemStream.Length - chunkMemStream.Position >= 8 && retval.UncompressedSize == 0xffffffff)
                    retval.UncompressedSize = chunkReader.ReadUInt64();
                if (chunkMemStream.Length - chunkMemStream.Position >= 8 && retval.CompressedSize == 0xffffffff)
                    retval.CompressedSize = chunkReader.ReadUInt64();
                if (chunkMemStream.Length - chunkMemStream.Position >= 8 && retval.LocalFileHeaderOffset == 0xffffffff)
                    retval.LocalFileHeaderOffset = chunkReader.ReadUInt64();
                if (chunkMemStream.Length - chunkMemStream.Position >= 4 && retval.FileStartDisk == 0xffff)
                    retval.FileStartDisk = chunkReader.ReadUInt32();
            }

            i += 4 + chunkSize;
        }

        retval.Size = 46 + retval.FileNameLength + retval.ExtraFieldLength + retval.CommentFieldLength;
        return retval;
    }
    
    public static LocalFileHeader ParseLocalFileHeader(byte[] buf, int offset, int size)
    {
        using var reader = new BinaryReader(new MemoryStream(buf, offset, size));
        if (!reader.ReadBytes(4).SequenceEqual(SigLocalFileHeader))
            throw new Exception("bad local file header signature");

        var retval = new LocalFileHeader();
        retval.VersionNeededToExtract = reader.ReadUInt16();
        retval.Flags = reader.ReadUInt16();
        retval.CompressionMethod = reader.ReadUInt16();
        retval.LastModificationTime = reader.ReadUInt16();
        retval.LastModificationDate = reader.ReadUInt16();
        retval.DataCrc32 = reader.ReadUInt32();
        retval.CompressedSize = reader.ReadUInt32();
        retval.UncompressedSize = reader.ReadUInt32();
        retval.FileNameLength = reader.ReadUInt16();
        retval.ExtraFieldLength = reader.ReadUInt16();

        retval.Size = 30 + retval.FileNameLength + retval.ExtraFieldLength;
        return retval;
    }
}