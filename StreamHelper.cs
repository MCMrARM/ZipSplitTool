using System.Security.Cryptography;

namespace ZipSplitTool;

public static class StreamHelper
{
    public static void CopyData(Stream from, Stream to, long length)
    {
        Span<byte> buf = stackalloc byte[4 * 1024 * 1024];
        while (length > 0)
        {
            var read = from.Read(buf[..(int)Math.Min(length, buf.Length)]);
            to.Write(buf[..read]);
            length -= read;
        }
    }

    public static byte[] HashData(Stream stream, long size)
    {
        byte[] buf = new byte[4 * 1024 * 1024];
        using var hash = SHA256.Create();
        while (size > 0)
        {
            var read = stream.Read(buf, 0, (int)Math.Min(size, buf.Length));
            if (hash.TransformBlock(buf, 0, read, null, 0) != read)
                throw new Exception("failed to calc sha256: TransformBlock failed");
            size -= read;
        }

        hash.TransformFinalBlock(buf, 0, 0);
        return hash.Hash!;
    }
}