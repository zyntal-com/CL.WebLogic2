using System.Buffers.Binary;
using CL.WebLogic.Forms.Uploads;

namespace CL.WebLogic.Tests.Forms;

public sealed class UploadPrimitivesTests
{
    public static byte[] Png(int width, int height)
    {
        var data = new byte[33];
        ((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]).CopyTo(data);
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(8), 13);
        "IHDR"u8.CopyTo(data.AsSpan(12));
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(16), width);
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(20), height);
        return data;
    }

    private static byte[] Gif(int width, int height)
    {
        var data = new byte[13];
        "GIF89a"u8.CopyTo(data);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(6), (ushort)width);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(8), (ushort)height);
        return data;
    }

    private static byte[] Bmp(int width, int height)
    {
        var data = new byte[54];
        data[0] = (byte)'B';
        data[1] = (byte)'M';
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(18), width);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(22), -height); // top-down bitmap
        return data;
    }

    private static byte[] WebPExtended(int width, int height)
    {
        var data = new byte[30];
        "RIFF"u8.CopyTo(data);
        "WEBP"u8.CopyTo(data.AsSpan(8));
        "VP8X"u8.CopyTo(data.AsSpan(12));
        WriteUInt24(data.AsSpan(24), width - 1);
        WriteUInt24(data.AsSpan(27), height - 1);
        return data;
    }

    private static byte[] Jpeg(int width, int height)
    {
        var data = new List<byte> { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };
        data.AddRange(new byte[14]); // APP0 payload
        data.AddRange([0xFF, 0xC0, 0x00, 0x11, 0x08, (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width, 0x03]);
        data.AddRange(new byte[9]);
        return [.. data];
    }

    private static void WriteUInt24(Span<byte> target, int value)
    {
        target[0] = (byte)value;
        target[1] = (byte)(value >> 8);
        target[2] = (byte)(value >> 16);
    }

    [Fact]
    public void Reads_image_dimensions_without_an_image_library()
    {
        Assert.True(ImageHeaderReader.TryRead(Png(640, 480), out var png));
        Assert.Equal(new ImageInfo("image/png", 640, 480), png);
        Assert.True(ImageHeaderReader.TryRead(Gif(32, 16), out var gif));
        Assert.Equal(new ImageInfo("image/gif", 32, 16), gif);
        Assert.True(ImageHeaderReader.TryRead(Bmp(100, 50), out var bmp));
        Assert.Equal(new ImageInfo("image/bmp", 100, 50), bmp);
        Assert.True(ImageHeaderReader.TryRead(WebPExtended(1920, 1080), out var webp));
        Assert.Equal(new ImageInfo("image/webp", 1920, 1080), webp);
        Assert.True(ImageHeaderReader.TryRead(Jpeg(800, 600), out var jpeg));
        Assert.Equal(new ImageInfo("image/jpeg", 800, 600), jpeg);
    }

    [Fact]
    public void Rejects_truncated_and_foreign_data()
    {
        Assert.False(ImageHeaderReader.TryRead(Png(1, 1).AsSpan(0, 20), out _));
        Assert.False(ImageHeaderReader.TryRead(Jpeg(10, 10).AsSpan(0, 12), out _));
        Assert.False(ImageHeaderReader.TryRead("%PDF-1.7"u8, out _));
        Assert.False(ImageHeaderReader.TryRead(Png(0, 10), out _));
        Assert.False(ImageHeaderReader.TryRead([], out _));
    }

    [Theory]
    [InlineData(".png", "image/png", true)]
    [InlineData(".jpg", "image/png", false)]
    [InlineData(".pdf", "text/plain", false)]
    [InlineData(".txt", "text/plain", true)]
    [InlineData(".docx", "application/zip", true)]
    [InlineData(".bin", "application/octet-stream", true)]
    public void Extension_must_match_content(string extension, string detected, bool expected) =>
        Assert.Equal(expected, FileSignatureSniffer.Matches(extension, detected));

    [Fact]
    public void Detects_content_types()
    {
        Assert.Equal("image/png", FileSignatureSniffer.Detect(Png(2, 2)));
        Assert.Equal("application/pdf", FileSignatureSniffer.Detect("%PDF-1.7\n"u8));
        Assert.Equal("application/zip", FileSignatureSniffer.Detect("PK\u0003\u0004rest"u8));
        Assert.Equal("text/plain", FileSignatureSniffer.Detect("hello, wørld"u8));
        Assert.Equal("application/octet-stream", FileSignatureSniffer.Detect([0x4D, 0x5A, 0x00, 0x90]));
    }

    [Theory]
    [InlineData("C:\\fakepath\\photo.png", "photo.png")]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("..hidden", "hidden")]
    [InlineData("a<b>c:d\"e|f?g*.txt", "abcdefg.txt")]
    [InlineData("line\nbreak.txt", "linebreak.txt")]
    [InlineData("", "file")]
    [InlineData(null, "file")]
    public void Cleans_file_names(string? input, string expected) =>
        Assert.Equal(expected, WebUploadChecker.CleanName(input));
}
