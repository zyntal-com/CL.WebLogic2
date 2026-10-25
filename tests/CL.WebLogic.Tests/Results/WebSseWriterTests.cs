using CL.WebLogic.Results;

namespace CL.WebLogic.Tests.Results;

public sealed class WebSseWriterTests
{
    [Fact]
    public void Format_WritesAllFields()
    {
        var text = WebSseWriter.Format(new WebSseEvent("hello", "greet", "7", 1500));

        Assert.Equal("id: 7\nevent: greet\nretry: 1500\ndata: hello\n\n", text);
    }

    [Fact]
    public void Format_SplitsMultilineData()
    {
        var text = WebSseWriter.Format(new WebSseEvent("a\r\nb\nc"));

        Assert.Equal("data: a\ndata: b\ndata: c\n\n", text);
    }

    [Fact]
    public void Format_StripsNewlinesFromSingleLineFields()
    {
        var text = WebSseWriter.Format(new WebSseEvent("x", "evil\nevent: injected", "1\r\n2"));

        Assert.Equal("id: 12\nevent: evilevent: injected\ndata: x\n\n", text);
    }
}
