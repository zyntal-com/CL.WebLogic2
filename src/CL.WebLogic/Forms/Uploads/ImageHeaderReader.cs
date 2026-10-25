using System.Buffers.Binary;

namespace CL.WebLogic.Forms.Uploads;

/// <summary>Reads the format and dimensions of PNG, JPEG, GIF, WebP and BMP images from their first bytes (no image library).</summary>
public static class ImageHeaderReader
{
    /// <summary>Reads <paramref name="data"/>; false when it is not a supported image or the header is cut off.</summary>
    public static bool TryRead(ReadOnlySpan<byte> data, out ImageInfo info)
    {
        info = default;
        if (data.Length >= 24 && data[..8].SequenceEqual((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
            return Make("image/png", BinaryPrimitives.ReadInt32BigEndian(data[16..]), BinaryPrimitives.ReadInt32BigEndian(data[20..]), out info);

        if (data.Length >= 10 && (data[..6].SequenceEqual("GIF87a"u8) || data[..6].SequenceEqual("GIF89a"u8)))
            return Make("image/gif", BinaryPrimitives.ReadUInt16LittleEndian(data[6..]), BinaryPrimitives.ReadUInt16LittleEndian(data[8..]), out info);

        if (data.Length >= 26 && data[0] == 'B' && data[1] == 'M')
            return Make("image/bmp", BinaryPrimitives.ReadInt32LittleEndian(data[18..]), Math.Abs(BinaryPrimitives.ReadInt32LittleEndian(data[22..])), out info);

        if (data.Length >= 30 && data[..4].SequenceEqual("RIFF"u8) && data[8..12].SequenceEqual("WEBP"u8))
            return ReadWebP(data, out info);

        if (data.Length >= 4 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
            return ReadJpeg(data, out info);
        return false;
    }

    private static bool ReadWebP(ReadOnlySpan<byte> data, out ImageInfo info)
    {
        info = default;
        var chunk = data[12..16];
        if (chunk.SequenceEqual("VP8 "u8) && data.Length >= 30)
            return Make("image/webp", BinaryPrimitives.ReadUInt16LittleEndian(data[26..]) & 0x3FFF, BinaryPrimitives.ReadUInt16LittleEndian(data[28..]) & 0x3FFF, out info);
        if (chunk.SequenceEqual("VP8L"u8) && data.Length >= 25)
        {
            int b0 = data[21], b1 = data[22], b2 = data[23], b3 = data[24];
            return Make("image/webp", 1 + (((b1 & 0x3F) << 8) | b0), 1 + (((b3 & 0x0F) << 10) | (b2 << 2) | ((b1 & 0xC0) >> 6)), out info);
        }
        if (chunk.SequenceEqual("VP8X"u8) && data.Length >= 30)
            return Make("image/webp", 1 + (data[24] | (data[25] << 8) | (data[26] << 16)), 1 + (data[27] | (data[28] << 8) | (data[29] << 16)), out info);
        return false;
    }

    private static bool ReadJpeg(ReadOnlySpan<byte> data, out ImageInfo info)
    {
        info = default;
        var position = 2;
        while (position + 9 < data.Length)
        {
            if (data[position] != 0xFF)
                return false;
            var marker = data[position + 1];
            if (marker == 0xFF)
            {
                position++; // fill byte
                continue;
            }
            var isFrame = marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC;
            if (isFrame)
                return Make("image/jpeg", BinaryPrimitives.ReadUInt16BigEndian(data[(position + 7)..]), BinaryPrimitives.ReadUInt16BigEndian(data[(position + 5)..]), out info);
            if (marker is 0xD8 or 0x01 or (>= 0xD0 and <= 0xD7))
            {
                position += 2;
                continue;
            }
            var length = BinaryPrimitives.ReadUInt16BigEndian(data[(position + 2)..]);
            if (length < 2)
                return false;
            position += 2 + length;
        }
        return false;
    }

    private static bool Make(string type, int width, int height, out ImageInfo info)
    {
        info = new ImageInfo(type, width, height);
        return width > 0 && height > 0;
    }
}
