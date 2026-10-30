using System.Globalization;
using CL.WebLogic.Configuration.Models;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Forms.Validation;
using CL.WebLogic.Localization.Services;
using CL.WebLogic.Testing.Services;
using CL.WebLogic.Theming.Services;

namespace CL.WebLogic.Tests.Localization;

public sealed class TextResourcesTests : IDisposable
{
    private readonly string _theme = Path.Combine(Path.GetTempPath(), "weblogic-locales", Guid.NewGuid().ToString("N"));
    private readonly WebLogicConfig _config = new()
    {
        Theme = new ThemeConfig { TemplateChangeCheckSeconds = 2 },
        Localization = new LocalizationConfig { Enabled = true, DefaultCulture = "en", SupportedCultures = ["sv-SE", "sv"] }
    };

    public TextResourcesTests() => Directory.CreateDirectory(Path.Combine(_theme, "locales"));

    public void Dispose() => Directory.Delete(_theme, recursive: true);

    private sealed class FixedRoot(string path) : IWebThemeRoot
    {
        public string RootPath => path;
    }

    private void Write(string culture, string json) => File.WriteAllText(Path.Combine(_theme, "locales", culture + ".json"), json);

    private readonly ManualTimeProvider _time = new();

    private (WebResourceTextLocalizer Localizer, ThemeJsonTextResources Resources, FakeWebLogicContext Context) Create()
    {
        var context = new FakeWebLogicContext(_config);
        var holder = new WebLogicConfigHolder(_config);
        var resources = new ThemeJsonTextResources(new FixedRoot(_theme), holder, context, _time);
        return (new WebResourceTextLocalizer(holder, resources), resources, context);
    }

    [Fact]
    public void Fallback_chain_walks_parents_then_the_default()
    {
        Assert.Equal(["sv-SE", "sv", "en-US", "en"], WebCultureFallback.Chain("sv-SE", "en-US"));
        Assert.Equal(["sv-Latn-SE", "sv-Latn", "sv"], WebCultureFallback.Chain("sv-Latn-SE", "sv"));
        Assert.Equal(["en"], WebCultureFallback.Chain("", "en"));
    }

    [Fact]
    public void Texts_fall_back_from_region_to_language_to_default_to_key()
    {
        Write("sv-SE", """{ "greeting": "Hej (SE)" }""");
        Write("sv", """{ "greeting": "Hej", "bye": "Hejdå" }""");
        Write("en", """{ "greeting": "Hello", "bye": "Bye", "only.en": "English only" }""");
        var (localizer, _, _) = Create();
        var swedish = CultureInfo.GetCultureInfo("sv-SE");

        Assert.Equal("Hej (SE)", localizer.GetText("greeting", swedish));
        Assert.Equal("Hejdå", localizer.GetText("bye", swedish));
        Assert.Equal("English only", localizer.GetText("only.en", swedish));
        Assert.Equal("missing.key", localizer.GetText("missing.key", swedish));
    }

    [Fact]
    public void Nested_objects_are_flattened_and_feed_form_messages()
    {
        Write("sv", """{ "forms": { "required": "{0} måste fyllas i.", "count": 3 } }""");
        var (localizer, resources, _) = Create();

        Assert.Equal("{0} måste fyllas i.", resources.GetText("sv", "forms.required"));
        Assert.Null(resources.GetText("sv", "forms.count"));

        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("sv-SE");
        try
        {
            var messages = new WebFormMessages(localizer);
            Assert.Equal("Namn måste fyllas i.", messages.Format("required", "Namn"));
            Assert.Equal("Namn must be at most 3 characters.", messages.Format("maxLength", "Namn", 3));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void Disabled_localization_returns_the_key_without_reading_files()
    {
        Write("en", """{ "greeting": "Hello" }""");
        _config.Localization.Enabled = false;
        var (localizer, _, _) = Create();

        Assert.Equal("greeting", localizer.GetText("greeting", CultureInfo.GetCultureInfo("en")));
    }

    [Fact]
    public void Changed_files_are_reloaded_and_broken_files_keep_the_last_good_texts()
    {
        var path = Path.Combine(_theme, "locales", "en.json");
        Write("en", """{ "greeting": "Hello" }""");
        var (_, resources, context) = Create();
        Assert.Equal("Hello", resources.GetText("en", "greeting"));

        Write("en", """{ "greeting": "Hi there" }""");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));
        Assert.Equal("Hello", resources.GetText("en", "greeting")); // re-checked at most every TemplateChangeCheckSeconds
        _time.Advance(TimeSpan.FromSeconds(3));
        Assert.Equal("Hi there", resources.GetText("en", "greeting"));

        Write("en", """{ "greeting": """);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(2));
        _time.Advance(TimeSpan.FromSeconds(3));
        Assert.Equal("Hi there", resources.GetText("en", "greeting"));
        Assert.True(context.Log.Contains(CodeLogic.Core.Logging.LogLevel.Warning, "could not be read"));
    }

    [Theory]
    [InlineData("../en")]
    [InlineData("..")]
    [InlineData("en/../../x")]
    [InlineData("")]
    public void Culture_names_cannot_leave_the_locales_folder(string culture)
    {
        File.WriteAllText(Path.Combine(_theme, "x.json"), """{ "k": "v" }""");
        var (_, resources, _) = Create();
        Assert.Null(resources.GetText(culture, "k"));
    }
}
