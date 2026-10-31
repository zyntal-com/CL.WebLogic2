using CL.WebLogic.Results;
using CodeLogic.Core.Results;

namespace CL.WebLogic.Tests.Results;

public sealed class WebErrorStatusTests
{
    public static TheoryData<Error, int> Kinds => new()
    {
        { Error.Validation("x.validation", "bad"), 400 },
        { Error.Unauthorized("x.unauthorized", "who"), 401 },
        { Error.NotFound("x.not_found", "gone"), 404 },
        { Error.Conflict("x.conflict", "clash"), 409 },
        { Error.Unavailable("x.unavailable", "full"), 503 },
        { Error.Timeout("x.timeout", "slow"), 504 },
        { Error.Internal("x.internal", "boom"), 500 },
    };

    [Theory]
    [MemberData(nameof(Kinds))]
    public void For_MapsTheErrorKind(Error error, int status) =>
        Assert.Equal(status, WebErrorStatus.For(error));

    [Fact]
    public void Problem_UsesTheKindNotTheCode()
    {
        // An app store picks the kind; its own code must not matter.
        var problem = WebResult.Problem(Error.Conflict("myapp.version_mismatch", "Reload the page."));

        Assert.Equal(409, problem.StatusCode);
        Assert.Equal("Reload the page.", problem.Details.Title);
    }

    [Fact]
    public void Problem_NeverSendsAnInternalErrorMessage()
    {
        var problem = WebResult.Problem(Error.Internal("db.failed", "Access denied for user 'root'@'10.0.0.5'"));

        Assert.Equal(500, problem.StatusCode);
        Assert.Equal(WebErrorStatus.InternalDetail, problem.Details.Title);
        Assert.DoesNotContain("root", problem.Details.Title ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public void Problem_TreatsADefaultResultAsInternal()
    {
        // A default Result reports IsFailure with a null Error; it must not throw or leak anything.
        var empty = default(Result<int>);
        var problem = WebResult.Problem(empty.Error);

        Assert.True(empty.IsFailure);
        Assert.Equal(500, problem.StatusCode);
        Assert.Equal(WebErrorStatus.InternalDetail, problem.Details.Title);
    }
}
