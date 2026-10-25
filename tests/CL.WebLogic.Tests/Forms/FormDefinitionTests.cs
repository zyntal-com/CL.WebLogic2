using CL.WebLogic.Forms.Models;
using CL.WebLogic.Forms.Services;

namespace CL.WebLogic.Tests.Forms;

public sealed class FormDefinitionTests
{
    [Fact]
    public void Builds_fields_from_annotations_and_attributes()
    {
        var form = WebFormDefinitionBuilder.Build(typeof(ContactForm));

        Assert.Equal("contact", form.Id);
        Assert.Equal("Contact us", form.Title);
        Assert.Equal("Send", form.SubmitLabel);
        Assert.Equal(["Name", "Email", "Topic", "InvoiceNumber", "Priority", "Message", "Subscribe", "Colors", "Code"], form.Fields.Select(f => f.Name));

        var name = form.Field("Name")!;
        Assert.True(name.Required);
        Assert.Equal(2, name.MinLength);
        Assert.Equal(50, name.MaxLength);

        Assert.Equal(WebFieldType.Email, form.Field("Email")!.Type);
        Assert.Equal("email", form.Field("Email")!.Autocomplete);
        Assert.Equal(WebFieldType.Select, form.Field("Topic")!.Type);
        Assert.Equal("Billing question", form.Field("Topic")!.Options[1].Label);
        Assert.Equal("Billing", form.Field("InvoiceNumber")!.ShowWhen!.Values[0]);
        Assert.Equal(("1", "10"), (form.Field("Priority")!.Min, form.Field("Priority")!.Max));
        Assert.Equal(WebFieldType.Number, form.Field("Priority")!.Type);
        Assert.Equal(WebFieldType.TextArea, form.Field("Message")!.Type);
        Assert.Equal(WebFieldType.Checkbox, form.Field("Subscribe")!.Type);
        Assert.Equal(WebFieldType.CheckboxList, form.Field("Colors")!.Type);
        Assert.Equal("Green", form.Field("Colors")!.Options[1].Label);
        Assert.Null(form.Field("IsAdmin"));
        Assert.Equal("Invoice number", form.Field("InvoiceNumber")!.Label);
    }

    [Fact]
    public void Builds_repeaters_steps_and_files()
    {
        var order = WebFormDefinitionBuilder.Build(typeof(OrderForm));
        var lines = order.Field("Lines")!;
        Assert.Equal(WebFieldType.Repeater, lines.Type);
        Assert.Equal((1, 3), (lines.RepeaterMin, lines.RepeaterMax));
        Assert.Equal(["Product", "Quantity"], lines.Repeater!.Fields.Select(f => f.Name));

        var wizard = WebFormDefinitionBuilder.Build(typeof(WizardForm));
        Assert.Equal(["account", "profile"], wizard.Steps.Select(s => s.Name));
        Assert.Equal(["DisplayName", "Bio"], wizard.FieldsOf("profile").Select(f => f.Name));

        var upload = WebFormDefinitionBuilder.Build(typeof(UploadForm));
        Assert.Equal(WebFieldType.File, upload.Field("Avatar")!.Type);
        Assert.Equal([".png", ".jpg"], upload.Field("Avatar")!.File!.Extensions);
        Assert.False(upload.Field("Avatar")!.IsMultiple);
        Assert.True(upload.Field("Attachments")!.IsMultiple);
        Assert.Equal(2, upload.Field("Attachments")!.File!.MaxFiles);
    }

    [Theory]
    [InlineData(typeof(ContactForm), "contact")]
    [InlineData(typeof(OrderLine), "order-line")]
    [InlineData(typeof(SignupForm), "signup")]
    public void Default_ids_are_kebab_case_without_suffix(Type type, string expected) =>
        Assert.Equal(expected, WebFormDefinitionBuilder.DefaultId(type));

    [Fact]
    public void Configurators_apply_after_attributes()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<IWebFormConfigurator<ContactForm>, ContactConfigurator>(services);
        var provider = new WebFormDefinitionProvider(Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(services));

        var form = provider.Get<ContactForm>();

        Assert.Equal("Your name", form.Field("Name")!.Label);
        Assert.Equal("Name", form.Fields[^1].Name);
        Assert.False(form.AntiBot);
        Assert.Same(form, provider.Get<ContactForm>());
    }

    private sealed class ContactConfigurator : IWebFormConfigurator<ContactForm>
    {
        public void Configure(WebFormBuilder<ContactForm> form)
        {
            form.WithoutAntiBot().Field(x => x.Name).Label("Your name").Order(int.MaxValue);
            Assert.Throws<ArgumentException>(() => form.Field(x => x.Name).Attribute("onclick", "x"));
        }
    }
}
