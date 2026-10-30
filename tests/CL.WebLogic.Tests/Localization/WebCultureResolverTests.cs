using CL.WebLogic.Configuration.Models;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Localization.Models;
using CL.WebLogic.Localization.Services;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Tests.Localization;

public sealed class WebCultureResolverTests
{
    private static WebCultureResolver Resolver(Action<LocalizationConfig>? configure = null)
    {
        var config = new WebLogicConfig
        {
            Localization = new LocalizationConfig { Enabled = true, DefaultCulture = "en", SupportedCultures = ["sv-SE", "da", "en-GB"] }
        };
        configure?.Invoke(config.Localization);
        return new WebCultureResolver(new WebLogicConfigHolder(config));
    }

    private static DefaultHttpContext Request(string? query = null, string? cookie = null, string? acceptLanguage = null)
    {
        var http = new DefaultHttpContext();
        if (query is not null)
            http.Request.QueryString = new QueryString("?culture=" + Uri.EscapeDataString(query));
        if (cookie is not null)
            http.Request.Headers.Cookie = "wl_culture=" + cookie;
        if (acceptLanguage is not null)
            http.Request.Headers.AcceptLanguage = acceptLanguage;
        return http;
    }

    [Fact]
    public void Disabled_resolves_nothing() =>
        Assert.Null(Resolver(l => l.Enabled = false).Resolve(Request(query: "sv-SE")));

    [Fact]
    public void Sources_are_checked_in_the_configured_order()
    {
        var http = Request(query: "da", cookie: "sv-SE", acceptLanguage: "en-GB");

        var resolved = Resolver().Resolve(http)!;
        Assert.Equal(("da", WebCultureSources.Query), (resolved.Name, resolved.Source));

        resolved = Resolver(l => l.Sources = ["cookie", "query", "header"]).Resolve(http)!;
        Assert.Equal(("sv-SE", WebCultureSources.Cookie), (resolved.Name, resolved.Source));

        resolved = Resolver(l => l.Sources = ["header"]).Resolve(http)!;
        Assert.Equal(("en-GB", WebCultureSources.Header), (resolved.Name, resolved.Source));
    }

    [Fact]
    public void An_unsupported_source_value_falls_through_to_the_next_source()
    {
        var resolved = Resolver().Resolve(Request(query: "fr", cookie: "de", acceptLanguage: "da;q=0.5"))!;
        Assert.Equal(("da", WebCultureSources.Header), (resolved.Name, resolved.Source));
    }

    [Fact]
    public void Nothing_supported_gives_the_default()
    {
        var resolved = Resolver().Resolve(Request(acceptLanguage: "fr-FR, de;q=0.8"))!;
        Assert.Equal(("en", WebCultureSources.Default), (resolved.Name, resolved.Source));
    }

    [Fact]
    public void Accept_language_is_ordered_by_quality()
    {
        var resolved = Resolver().Resolve(Request(acceptLanguage: "fr;q=0.9, da;q=0.4, sv-SE;q=0.8, *;q=1"))!;
        Assert.Equal("sv-SE", resolved.Name);
        Assert.Equal(["en", "da"], WebCultureResolver.AcceptLanguages("en, da;q=0, da;q=0.1, *").Take(2));
    }

    [Theory]
    [InlineData("sv-SE", "sv-SE")]
    [InlineData("SV-se", "sv-SE")]
    [InlineData("sv_SE", "sv-SE")]
    [InlineData("sv", "sv-SE")]       // same language
    [InlineData("sv-FI", "sv-SE")]    // same language
    [InlineData("da-DK", "da")]       // parent
    [InlineData("en-US", "en")]       // parent is the default
    [InlineData("en-GB", "en-GB")]
    public void Candidates_match_only_supported_cultures(string candidate, string expected)
    {
        Assert.True(Resolver().TryMatch(candidate, out var culture));
        Assert.Equal(expected, culture.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("fr")]
    [InlineData("de-DE")]
    [InlineData("xx-YY")]
    [InlineData("sv;DROP")]
    [InlineData("sv SE")]
    [InlineData("../sv")]
    [InlineData("sv\u0000")]
    [InlineData("sv-ＳＥ")]
    public void Unsupported_and_garbage_candidates_are_rejected(string? candidate) =>
        Assert.False(Resolver().TryMatch(candidate, out _));

    [Fact]
    public void Huge_values_are_rejected_without_work()
    {
        Assert.False(Resolver().TryMatch("sv-" + new string('a', 5000), out _));
        var resolved = Resolver().Resolve(Request(acceptLanguage: new string('x', 100_000) + ",sv-SE"))!;
        Assert.Equal(WebCultureSources.Default, resolved.Source); // entries past the length cap are ignored
        Assert.Equal(16, WebCultureResolver.AcceptLanguages(string.Join(',', Enumerable.Repeat("fr", 500))).Count());
    }

    [Fact]
    public void Https_cookie_name_keeps_the_host_prefix()
    {
        var http = new DefaultHttpContext();
        http.Request.Scheme = "https";
        http.Request.Headers.Cookie = "__Host-wl_culture=da";
        Assert.Equal("da", Resolver().Resolve(http)!.Name);
    }
}
