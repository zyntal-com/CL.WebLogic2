using CL.WebLogic.ClientBundler.Lexing;
using CL.WebLogic.ClientBundler.Linking;

namespace CL.WebLogic.ClientBundler.Tests;

public sealed class JsLexerTests
{
    private static List<JsToken> Significant(string source) => [.. JsLexer.Tokenize(source).Where(t => !t.IsTrivia)];

    [Theory]
    [InlineData("const r = /a\\/b[/]c/gi;", "/a\\/b[/]c/gi")]
    [InlineData("return /x/.test(s)", "/x/")]
    [InlineData("f(/=/)", "/=/")]
    [InlineData("[1, /y/]", "/y/")]
    public void RegexLiterals_AreRecognized(string source, string regex) =>
        Assert.Contains(Significant(source), t => t.Kind == JsTokenKind.RegExp && t.Text == regex);

    [Theory]
    [InlineData("a / b / c")]
    [InlineData("x() / 2 / y")]
    [InlineData("arr[0] / 2")]
    public void Division_IsNotARegex(string source) =>
        Assert.DoesNotContain(Significant(source), t => t.Kind == JsTokenKind.RegExp);

    [Fact]
    public void CommentMarkers_InsideLiterals_AreText()
    {
        var tokens = Significant("const a = '// not a comment'; const b = \"/* nor this */\"; const c = `x ${ '`' + `/*${1}*/` } y`;");
        Assert.Equal(3, tokens.Count(t => t.Kind is JsTokenKind.String or JsTokenKind.Template));
        Assert.Contains(tokens, t => t.Kind == JsTokenKind.Template && t.Text == "`x ${ '`' + `/*${1}*/` } y`");
    }

    [Fact]
    public void NestedTemplateBraces_Balance()
    {
        var tokens = Significant("const t = `a${ { b: `c${ {d:1}.d }` }.b }e`; next();");
        Assert.Equal("`a${ { b: `c${ {d:1}.d }` }.b }e`", tokens.Single(t => t.Kind == JsTokenKind.Template).Text);
        Assert.Contains(tokens, t => t.IsWord("next"));
    }

    [Theory]
    [InlineData("'abc")]
    [InlineData("`abc")]
    [InlineData("/* abc")]
    [InlineData("x = /abc")]
    public void Unterminated_Throws(string source) => Assert.Throws<BundleException>(() => JsLexer.Tokenize(source));

    [Fact]
    public void Lines_AreTracked() =>
        Assert.Equal(3, Significant("a\n/* x\n */ b").Single(t => t.IsWord("b")).Line);
}
