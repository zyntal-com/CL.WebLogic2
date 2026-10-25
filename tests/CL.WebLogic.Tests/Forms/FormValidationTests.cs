using CL.WebLogic.Forms.Models;
using CL.WebLogic.Forms.Services;

namespace CL.WebLogic.Tests.Forms;

public sealed class FormValidationTests
{
    [Fact]
    public void Required_lengths_and_formats()
    {
        var (_, errors) = FormTestServices.BindAndValidate<ContactForm>(("Name", "A"), ("Email", "not-an-email"), ("Priority", "11"));
        Assert.Contains("Name must be at least 2 characters.", errors.For("Name"));
        Assert.Contains("Email must be a valid e-mail address.", errors.For("Email"));
        Assert.Contains("Priority must be at most 10.", errors.For("Priority"));

        var (_, missing) = FormTestServices.BindAndValidate<ContactForm>();
        Assert.Contains("Name is required.", missing.For("Name"));
        Assert.Contains("Email is required.", missing.For("Email"));
    }

    [Fact]
    public void Length_counts_characters_not_utf16_units()
    {
        var (_, errors) = FormTestServices.BindAndValidate<ContactForm>(("Name", "😀😀"), ("Email", "a@b.co"));
        Assert.False(errors.Has("Name"));
    }

    [Fact]
    public void Pattern_must_match_the_whole_value()
    {
        Assert.False(FormTestServices.BindAndValidate<ContactForm>(("Name", "Ada"), ("Email", "a@b.co"), ("Code", "ABC-123")).Errors.Any);
        Assert.Contains("Code is not in the expected format.", FormTestServices.BindAndValidate<ContactForm>(("Code", "xABC-123x")).Errors.For("Code"));
    }

    [Fact]
    public void Catastrophic_patterns_time_out_instead_of_hanging()
    {
        var definition = WebFormDefinitionBuilder.Build(typeof(ContactForm));
        definition.Field("Code")!.Pattern = "(a+)+$";
        var values = FormTestServices.Values(("Code", new string('a', 40) + "!"));
        var errors = new WebFormErrors();
        var model = FormTestServices.Binder.Bind(definition, definition.Fields, values, _ => true, errors);

        var watch = System.Diagnostics.Stopwatch.StartNew();
        FormTestServices.Validator.Validate(definition, [definition.Field("Code")!], model, _ => true, errors);

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5));
        Assert.Contains("Code could not be checked. Try a shorter value.", errors.For("Code"));
    }

    [Fact]
    public void Compare_and_validatable_object()
    {
        var (_, errors) = FormTestServices.BindAndValidate<SignupForm>(("Password", "longenough"), ("Confirm", "different"));
        Assert.Contains("Repeat password must match Password.", errors.For("Confirm"));

        var (_, cross) = FormTestServices.BindAndValidate<CrossFieldForm>(("From", "2026-05-02"), ("To", "2026-05-01"));
        Assert.Contains("To must be after From.", cross.For("To"));
    }

    [Fact]
    public void Messages_come_from_the_localizer_when_it_has_them()
    {
        var messages = new CL.WebLogic.Forms.Validation.WebFormMessages(new DanishLocalizer());
        Assert.Equal("Navn skal udfyldes.", messages.Format("required", "Navn"));
        Assert.Equal("Navn must be at most 3 characters.", messages.Format("maxLength", "Navn", 3));
    }

    private sealed class DanishLocalizer : Localization.Services.IWebTextLocalizer
    {
        public string GetText(string key, System.Globalization.CultureInfo? culture = null) => key == "forms.required" ? "{0} skal udfyldes." : key;
    }
}
