using CL.WebLogic.Runtime.Models;

namespace CL.WebLogic.Tests.Runtime;

public sealed class WebPaginationTests
{
    [Fact]
    public void Create_ComputesPages()
    {
        var p = WebPagination.Create(2, 10, 25);

        Assert.Equal(2, p.Page);
        Assert.Equal(3, p.TotalPages);
        Assert.Equal(10, p.Skip);
        Assert.True(p.HasPrevious);
        Assert.True(p.HasNext);
        Assert.Equal(11, p.FirstItem);
        Assert.Equal(20, p.LastItem);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(9999, 3)]
    [InlineData(int.MaxValue, 3)]
    public void Create_ClampsPage(int requested, int expected) =>
        Assert.Equal(expected, WebPagination.Create(requested, 10, 25).Page);

    [Fact]
    public void Create_GuardsPageSize()
    {
        Assert.Equal(100, WebPagination.Create(1, 1_000_000, 5000).PageSize);
        Assert.Equal(1, WebPagination.Create(1, 0, 5).PageSize);
        Assert.Equal(20, WebPagination.Create(1, 50, 5000, maxPageSize: 20).PageSize);
        Assert.Throws<ArgumentOutOfRangeException>(() => WebPagination.Create(1, 10, 5, maxPageSize: 0));
    }

    [Fact]
    public void EmptyList_IsPageOneOfOne()
    {
        var p = WebPagination.Create(4, 10, 0);

        Assert.Equal(1, p.Page);
        Assert.Equal(1, p.TotalPages);
        Assert.False(p.HasPrevious);
        Assert.False(p.HasNext);
        Assert.Equal(0, p.FirstItem);
        Assert.Equal(0, p.LastItem);
        Assert.Equal(0, WebPagination.Create(1, 10, -3).TotalItems);
    }

    [Fact]
    public void HugeTotals_DoNotOverflow()
    {
        var p = WebPagination.Create(int.MaxValue, 1, long.MaxValue);

        Assert.Equal(int.MaxValue, p.TotalPages);
        Assert.Equal(int.MaxValue - 1L, p.Skip);
    }

    [Theory]
    [InlineData(null, 1)]
    [InlineData("", 1)]
    [InlineData("3", 3)]
    [InlineData("-2", 1)]
    [InlineData("+2", 1)]
    [InlineData("0", 1)]
    [InlineData("2.5", 1)]
    [InlineData("99999999999", 1)]
    [InlineData("½", 1)]
    public void ParsePage_AcceptsOnlyPositiveDigits(string? value, int expected) =>
        Assert.Equal(expected, WebPagination.ParsePage(value));

    [Fact]
    public void PagedResult_SlicesInMemoryList()
    {
        var all = Enumerable.Range(1, 23).ToList();
        var result = WebPagination.Create(3, 10, all.Count).Apply(all);

        Assert.Equal([21, 22, 23], result.Items);
        Assert.Equal(3, result.Pagination.Page);
        Assert.False(result.Pagination.HasNext);
    }
}
