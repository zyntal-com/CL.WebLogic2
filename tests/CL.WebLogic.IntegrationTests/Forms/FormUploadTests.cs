using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace CL.WebLogic.IntegrationTests.Forms;

public sealed class FormUploadTests
{
    private static byte[] Png(int width, int height, int size = 64)
    {
        var data = new byte[Math.Max(size, 33)];
        ((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]).CopyTo(data);
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(8), 13);
        "IHDR"u8.CopyTo(data.AsSpan(12));
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(16), width);
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(20), height);
        return data;
    }

    private static async Task<(HttpStatusCode Status, JsonElement Json)> UploadAsync(byte[] content, string fileName)
    {
        await using var host = await FormsTestSite.StartAsync();
        var page = await host.Client.GetStringAsync("/avatar");
        var multipart = new MultipartFormDataContent();
        foreach (var (key, value) in FormsTestSite.HiddenFields(page))
            multipart.Add(new StringContent(value), key);
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png"); // claimed type is never trusted
        multipart.Add(file, "Avatar", fileName);
        multipart.Add(new StringContent("hello"), "Caption");

        var response = await host.Client.SendAsync(FormsTestSite.AsClient(new HttpRequestMessage(HttpMethod.Post, "/avatar") { Content = multipart }));
        return (response.StatusCode, await response.Content.ReadFromJsonAsync<JsonElement>());
    }

    [Fact]
    public async Task Accepts_a_valid_image_and_reads_its_size()
    {
        var (status, json) = await UploadAsync(Png(48, 32), "C:\\fakepath\\me.png");

        Assert.Equal(HttpStatusCode.OK, status);
        var data = json.GetProperty("data");
        Assert.Equal("me.png", data.GetProperty("fileName").GetString());
        Assert.Equal("image/png", data.GetProperty("detectedType").GetString());
        Assert.Equal(48, data.GetProperty("width").GetInt32());
        Assert.Equal(32, data.GetProperty("height").GetInt32());
    }

    [Theory]
    [InlineData("fake.png", "Avatar: the content of fake.png does not match its type.")]
    [InlineData("me.exe", "Avatar: me.exe is not an allowed file type.")]
    public async Task Rejects_disguised_and_disallowed_files(string fileName, string message)
    {
        var (status, json) = await UploadAsync(Encoding.UTF8.GetBytes("<?php system($_GET['c']); ?>"), fileName);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.Equal(message, json.GetProperty("errors").GetProperty("Avatar")[0].GetString());
    }

    [Fact]
    public async Task Rejects_oversized_files_and_images()
    {
        var (_, big) = await UploadAsync(Png(10, 10, size: 4096), "big.png");
        Assert.Equal("Avatar: big.png is larger than 2 KB.", big.GetProperty("errors").GetProperty("Avatar")[0].GetString());

        var (_, wide) = await UploadAsync(Png(65, 10), "wide.png");
        Assert.Equal("Avatar: wide.png is larger than 64×64 pixels.", wide.GetProperty("errors").GetProperty("Avatar")[0].GetString());
    }
}
