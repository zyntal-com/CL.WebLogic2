using System.IO.Pipelines;
using System.Text;
using CL.WebLogic.Templates.Rendering;
using Microsoft.Extensions.ObjectPool;

namespace CL.WebLogic.Tests.Templates;

public sealed class RenderedTemplateTests
{
    private static readonly ObjectPool<StringBuilder> Pool = new DefaultObjectPoolProvider().CreateStringBuilderPool();

    private static async Task<byte[]> WriteAsync(RenderedTemplate page)
    {
        // Read while writing, as Kestrel does: a large page would otherwise stop at the pipe's back-pressure limit.
        var pipe = new Pipe();
        var reading = Task.Run(async () =>
        {
            using var buffer = new MemoryStream();
            await pipe.Reader.AsStream().CopyToAsync(buffer);
            return buffer.ToArray();
        });
        try
        {
            await page.WriteToAsync(pipe.Writer, CancellationToken.None);
        }
        finally
        {
            await pipe.Writer.CompleteAsync();
        }
        return await reading;
    }

    [Fact]
    public async Task A_surrogate_pair_split_between_chunks_is_encoded_whole()
    {
        // A tiny initial capacity makes the builder start a new chunk in the middle of the emoji.
        var builder = new StringBuilder(1);
        const string text = "ab😀cd😀é<b>ok</b>";
        builder.Append(text);
        var chunks = 0;
        foreach (var _ in builder.GetChunks())
            chunks++;
        Assert.True(chunks > 1);

        using var page = new RenderedTemplate(builder, Pool);

        Assert.Equal(Encoding.UTF8.GetBytes(text), await WriteAsync(page));
    }

    [Fact]
    public async Task A_page_larger_than_one_pipe_segment_is_written_completely()
    {
        var text = string.Concat(Enumerable.Range(0, 20_000).Select(i => $"<p>{i} é 😀</p>\n"));
        var builder = new StringBuilder();
        builder.Append(text);
        using var page = new RenderedTemplate(builder, Pool);

        Assert.Equal(Encoding.UTF8.GetBytes(text), await WriteAsync(page));
    }

    [Fact]
    public async Task Text_pages_and_empty_pages_write_the_same_bytes_as_UTF8()
    {
        using var text = RenderedTemplate.FromText("héllo 😀");
        Assert.Equal(Encoding.UTF8.GetBytes("héllo 😀"), await WriteAsync(text));
        Assert.Equal("héllo 😀", text.ToString());

        using var empty = RenderedTemplate.FromText("");
        Assert.Empty(await WriteAsync(empty));
    }

    [Fact]
    public async Task A_disposed_page_cannot_be_written_and_its_builder_goes_back_to_the_pool()
    {
        var builder = Pool.Get();
        builder.Append("x");
        var page = new RenderedTemplate(builder, Pool);
        page.Dispose();
        page.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await WriteAsync(page));
        Assert.Equal(0, builder.Length);
    }
}
