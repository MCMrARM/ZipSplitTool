namespace ZipSplitTool;

public class ZipArchive
{
    public ZipFormat.Eocd eocd;
    public long eocd64LocatorOffset;
    public ZipFormat.Eocd64Locator eocd64Locator;
    public ZipFormat.Eocd64 eocd64;


    public void Clear()
    {
        eocd = new ZipFormat.Eocd();
        eocd64Locator = new ZipFormat.Eocd64Locator();
        eocd64 = new ZipFormat.Eocd64();
    }

    public void ReadHeaders(FileStream file)
    {
        Clear();

        ReadEocd(file);
        if (eocd.IsZip64)
        {
            ReadEocd64Locator(file);
            ReadEocd64(file);
        }
    }

    private void ReadEocd(Stream file)
    {
        long length = file.Length;
        long tryLen = Math.Min(512, length);

        file.Seek(length - tryLen, SeekOrigin.Begin);
        byte[] buf = new byte[tryLen];
        if (file.Read(buf) != tryLen)
            throw new Exception("failed to read bytes for EOCD");

        var sig = ZipFormat.SigEocd;
        int i;
        for (i = buf.Length - 3; i >= 0; --i)
        {
            if (buf[i] == sig[0] && buf[i + 1] == sig[1] && buf[i + 2] == sig[2] && buf[i + 3] == sig[3])
                break;
        }

        if (i < 0)
            throw new Exception("failed to find EOCD");

        eocd = ZipFormat.ParseEocd(buf, i, buf.Length - i);
    }

    private void ReadEocd64Locator(Stream file)
    {
        var locatorSize = ZipFormat.Eocd64Locator.Size;
        eocd64LocatorOffset = file.Length - eocd.Size - locatorSize;
        file.Seek(eocd64LocatorOffset, SeekOrigin.Begin);
        var buf = new byte[locatorSize];
        if (file.Read(buf) != locatorSize)
            throw new Exception("failed to read bytes for EOCD64 locator");

        eocd64Locator = ZipFormat.ParseEocd64Locator(buf, 0, locatorSize);
    }

    public void ReadEocd64(Stream file)
    {
        var minSize = ZipFormat.Eocd64.MinSizeForSizeRead;
        file.Seek((long)eocd64Locator.EocdOffset, SeekOrigin.Begin);
        var buf = new byte[minSize];
        if (file.Read(buf) != minSize)
            throw new Exception("failed to read bytes for EOCD64 header size");
        var size = ZipFormat.GetEocd64Size(buf, 0, minSize);
        Array.Resize(ref buf, (int)size);
        if (file.Read(buf, minSize, (int)(size - minSize)) != size - minSize)
            throw new Exception("failed to read bytes for EOCD64 header");

        eocd64 = ZipFormat.ParseEocd64(buf, 0, (int)size);
    }

    public IEnumerable<ZipFormat.CentralDirectoryFileHeader> ReadFileHeaders(Stream file, int bufSize = 4 * 1024 * 1024)
    {
        byte[] buf = new byte[bufSize];
        int off = 0;
        long fileTotalOff = (long) (eocd.IsZip64 ? eocd64.CentralDirectoryOffset : eocd.CentralDirectoryOffset);
        ulong remainingEntries = eocd.IsZip64 ? eocd64.CentralDirectoryThisDiskRecords : eocd.CentralDirectoryThisDiskRecords;
        while (remainingEntries > 0)
        {
            file.Seek(fileTotalOff, SeekOrigin.Begin);
            int read = file.Read(buf, off, buf.Length - off) + off;
            fileTotalOff += read;
            int i = 0;
            while (i < read && remainingEntries > 0)
            {
                ZipFormat.CentralDirectoryFileHeader centralDir;
                try
                {
                    centralDir = ZipFormat.ParseCentralDirectoryFileHeader(buf, i, read - i);
                }
                catch (EndOfStreamException)
                {
                    break;
                }

                yield return centralDir;
                i += centralDir.Size;
                --remainingEntries;
            }

            if (i == 0 || remainingEntries == 0)
                break;
            Array.Copy(buf, i, buf, 0, read - i);
            off = read;
        }
    }

    public static ZipFormat.LocalFileHeader ReadLocalFileHeader(Stream file)
    {
        var data = new byte[ZipFormat.LocalFileHeader.MinSize];
        if (file.Read(data) != ZipFormat.LocalFileHeader.MinSize)
            throw new Exception("failed to read bytes for local header");
        return ZipFormat.ParseLocalFileHeader(data, 0, data.Length);
    }
}