using CL.WebLogic.Forms.Models;
using CL.WebLogic.Forms.Services;

namespace CL.WebLogic.Tests.Forms;

public sealed class FormBindingTests
{
    [Fact]
    public void Binds_values_with_invariant_conversion()
    {
        var (model, errors) = FormTestServices.BindAndValidate<ContactForm>(
            ("Name", "  Ada  "), ("Email", "ada@example.com"), ("Topic", "Support"), ("Priority", "3"),
            ("Subscribe", "true"), ("Colors", "red"), ("Colors", "blue"), ("Colors", "red"), ("Message", "  keep spacing "));

        Assert.False(errors.Any, string.Join(", ", errors.Fields.Keys));
        Assert.Equal("Ada", model.Name);
        Assert.Equal(Topic.Support, model.Topic);
        Assert.Equal(3, model.Priority);
        Assert.True(model.Subscribe);
        Assert.Equal(["red", "blue"], model.Colors);
        Assert.Equal("  keep spacing ", model.Message);
    }

    [Fact]
    public void Ignores_undeclared_and_ignored_fields()
    {
        var (model, _) = FormTestServices.BindAndValidate<ContactForm>(("Name", "Ada"), ("IsAdmin", "true"), ("Unknown", "x"));
        Assert.False(model.IsAdmin);
    }

    [Fact]
    public void Unchecked_checkbox_with_hidden_fallback_binds_false()
    {
        var (model, _) = FormTestServices.BindAndValidate<ContactForm>(("Subscribe", "false"));
        Assert.False(model.Subscribe);
    }

    [Theory]
    [InlineData("Priority", "three", "Priority must be a number.")]
    [InlineData("Topic", "1", "Topic is not valid.")]
    [InlineData("Topic", "Nope", "Topic is not valid.")]
    [InlineData("Colors", "purple", "Colors contains a choice that is not available.")]
    public void Conversion_and_choice_failures_are_errors(string field, string value, string message)
    {
        var (_, errors) = FormTestServices.BindAndValidate<ContactForm>((field, value));
        Assert.Contains(message, errors.For(field));
    }

    [Fact]
    public void Hidden_conditional_fields_are_not_bound_or_required()
    {
        var (model, errors) = FormTestServices.BindAndValidate<ContactForm>(("Name", "Ada"), ("Email", "a@b.co"), ("Topic", "General"), ("InvoiceNumber", "INV-1"));
        Assert.Null(model.InvoiceNumber);
        Assert.False(errors.Any);

        var (shown, shownErrors) = FormTestServices.BindAndValidate<ContactForm>(("Name", "Ada"), ("Email", "a@b.co"), ("Topic", "Billing"));
        Assert.Null(shown.InvoiceNumber);
        Assert.Contains("Invoice number is required.", shownErrors.For("InvoiceNumber"));
    }

    [Fact]
    public void Binds_repeater_items_in_index_order()
    {
        var (model, errors) = FormTestServices.BindAndValidate<OrderForm>(
            ("Customer", "ACME"), ("Lines[5].Product", "B"), ("Lines[5].Quantity", "2"), ("Lines[1].Product", "A"), ("Lines[x].Product", "bad"));

        Assert.False(errors.Any);
        Assert.Equal(["A", "B"], model.Lines.Select(l => l.Product));
        Assert.Equal([1, 2], model.Lines.Select(l => l.Quantity));
    }

    [Fact]
    public void Repeater_errors_use_prefixed_keys_and_limits()
    {
        var (_, errors) = FormTestServices.BindAndValidate<OrderForm>(("Customer", "ACME"), ("Lines[0].Quantity", "500"));
        Assert.Contains("Product is required.", errors.For("Lines[0].Product"));
        Assert.Contains("Quantity must be at most 99.", errors.For("Lines[0].Quantity"));

        var (_, empty) = FormTestServices.BindAndValidate<OrderForm>(("Customer", "ACME"));
        Assert.Contains("Lines: add at least 1.", empty.For("Lines"));

        var (_, many) = FormTestServices.BindAndValidate<OrderForm>(
            ("Customer", "ACME"), ("Lines[0].Product", "A"), ("Lines[1].Product", "B"), ("Lines[2].Product", "C"), ("Lines[3].Product", "D"), ("Lines[4].Product", "E"));
        Assert.Contains("Lines: add at most 3.", many.For("Lines"));
    }

    [Fact]
    public void Rich_text_is_sanitized_on_bind()
    {
        var (model, _) = FormTestServices.BindAndValidate<XssForm>(("Body", "<p onclick=\"x()\">Hi<script>alert(1)</script></p>"));
        Assert.Equal("<p>Hi</p>", model.Body);
    }

    [Theory]
    [InlineData("2026-03-04", typeof(DateOnly))]
    [InlineData("13:45", typeof(TimeOnly))]
    [InlineData("1.5", typeof(decimal))]
    [InlineData("on", typeof(bool))]
    [InlineData("8a3f3c3e-0b5b-4f5e-9a51-6f1f7d7a9d11", typeof(Guid))]
    public void Converter_understands_browser_formats(string text, Type type)
    {
        Assert.True(WebValueConverter.TryConvert(text, type, out var value));
        Assert.IsType(type, value);
    }

    [Theory]
    [InlineData("NaN", typeof(double))]
    [InlineData("1,5", typeof(decimal))]
    [InlineData("04/03/2026", typeof(DateOnly))]
    [InlineData("maybe", typeof(bool))]
    public void Converter_rejects_other_formats(string text, Type type) =>
        Assert.False(WebValueConverter.TryConvert(text, type, out _));

    [Fact]
    public void Values_round_trip_through_the_model()
    {
        var definition = WebFormDefinitionBuilder.Build(typeof(OrderForm));
        var values = CL.WebLogic.Forms.Rendering.WebFormValues.FromModel(definition, new OrderForm { Customer = "ACME", Lines = [new() { Product = "A", Quantity = 4 }] });
        Assert.Equal("ACME", values["Customer"]);
        Assert.Equal("4", values["Lines[0].Quantity"]);
        Assert.False(CL.WebLogic.Forms.Rendering.WebFormValues.FromModel(WebFormDefinitionBuilder.Build(typeof(SignupForm)), new SignupForm { Password = "secret" }).ContainsKey("Password"));
    }
}
