using System.Text.Json;
using System.Text.RegularExpressions;
using CL.WebLogic.Forms.Models;
using CL.WebLogic.Forms.Rendering;
using CL.WebLogic.Forms.Services;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Tests.Forms;

public sealed partial class FormRenderingTests
{
    private static async Task<string> RenderAsync<T>(bool bootstrap = false, WebFormErrors? errors = null, int step = 0, params (string Key, string Value)[] values)
    {
        IWebFormRenderer renderer = bootstrap ? new Bootstrap5FormRenderer(FormTestServices.RendererServices()) : new PlainFormRenderer(FormTestServices.RendererServices());
        var http = new DefaultHttpContext();
        http.Request.Path = "/contact";
        return await renderer.RenderAsync(new WebFormRenderRequest
        {
            Definition = WebFormDefinitionBuilder.Build(typeof(T)),
            HttpContext = http,
            Values = FormTestServices.Values(values),
            Errors = errors,
            StepIndex = step
        }, CancellationToken.None);
    }

    [Fact]
    public async Task Renders_form_with_security_fields_and_schema()
    {
        var html = await RenderAsync<ContactForm>();

        Assert.Contains("<form id=\"wl-form-contact\" class=\"wl-form\" method=\"post\" action=\"/contact\" data-wl-form=\"contact\">", html);
        Assert.Contains("name=\"__wl_csrf\" value=\"csrf-token\"", html);
        Assert.Contains("name=\"__wl_hp\"", html);
        Assert.Contains("name=\"__wl_ts\"", html);
        Assert.DoesNotContain("enctype", html);

        var schema = JsonDocument.Parse(SchemaJson().Match(html).Groups[1].Value).RootElement;
        Assert.Equal("contact", schema.GetProperty("formId").GetString());
        var name = schema.GetProperty("fields").EnumerateArray().First(f => f.GetProperty("name").GetString() == "Name");
        Assert.True(name.GetProperty("required").GetBoolean());
        Assert.Equal(50, name.GetProperty("maxLength").GetInt32());
        Assert.Equal("{label} is required.", schema.GetProperty("messages").GetProperty("required").GetString());
    }

    [Fact]
    public async Task Renders_native_validation_attributes_and_labels()
    {
        var html = await RenderAsync<ContactForm>();

        Assert.Contains("<label class=\"wl-label\" for=\"wl-form-contact-Name\">Name <span aria-hidden=\"true\">*</span></label>", html);
        Assert.Contains("id=\"wl-form-contact-Name\" name=\"Name\" required minlength=\"2\" maxlength=\"50\"", html);
        Assert.Contains("type=\"email\"", html);
        Assert.Contains("autocomplete=\"email\"", html);
        Assert.Contains("min=\"1\" max=\"10\"", html);
        Assert.Contains("pattern=\"[A-Z]{3}-[0-9]{3}\"", html);
        Assert.Contains("<option value=\"Billing\">Billing question</option>", html);
        Assert.Contains("type=\"checkbox\" class=\"wl-check__input\" value=\"true\" id=\"wl-form-contact-Subscribe\"", html);
    }

    [Fact]
    public async Task Shows_values_and_errors_accessibly()
    {
        var errors = new WebFormErrors();
        errors.Add("Name", "Name is required.");
        errors.Add("", "Could not send.");
        var html = await RenderAsync<ContactForm>(errors: errors, values: [("Email", "a@b.co\" onfocus=\"x"), ("Colors", "green"), ("Topic", "Billing")]);

        Assert.Contains("aria-invalid=\"true\" aria-describedby=\"wl-err-wl-form-contact-Name\"", html);
        Assert.Contains("<div class=\"wl-field-error\" id=\"wl-err-wl-form-contact-Name\" data-wl-error=\"Name\">Name is required.</div>", html);
        Assert.Contains("role=\"alert\" data-wl-error=\"\">Could not send.</div>", html);
        Assert.Contains("value=\"a@b.co&quot; onfocus=&quot;x\"", html);
        Assert.Contains("value=\"green\" checked", html);
        Assert.Contains("<option value=\"Billing\" selected>", html);
    }

    [Fact]
    public async Task Hidden_conditional_fields_render_hidden()
    {
        var hidden = await RenderAsync<ContactForm>(values: [("Topic", "General")]);
        Assert.Matches("data-wl-field=\"InvoiceNumber\" data-wl-show-when=\"[^\"]+\" hidden>", hidden);

        var shown = await RenderAsync<ContactForm>(values: [("Topic", "Billing")]);
        Assert.Matches("data-wl-field=\"InvoiceNumber\" data-wl-show-when=\"[^\"]+\">", shown);
    }

    [Fact]
    public async Task Encodes_labels_help_and_schema()
    {
        var html = await RenderAsync<XssForm>();

        Assert.DoesNotContain("<script>alert", html);
        Assert.DoesNotContain("<img", html);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
        Assert.Equal(1, Regex.Count(html, "</script>"));
        Assert.Contains("data-wl-richtext", html);
    }

    [Fact]
    public async Task Never_renders_password_values()
    {
        var html = await RenderAsync<SignupForm>(values: [("Password", "hunter22"), ("Confirm", "hunter22")]);
        Assert.DoesNotContain("hunter22", html);
    }

    [Fact]
    public async Task Renders_repeaters_with_a_template()
    {
        var html = await RenderAsync<OrderForm>(values: [("Lines[0].Product", "A"), ("Lines[3].Product", "B")]);

        Assert.Contains("data-wl-repeater=\"Lines\" data-wl-repeater-min=\"1\" data-wl-repeater-max=\"3\"", html);
        Assert.Contains("name=\"Lines[0].Product\"", html);
        Assert.Contains("name=\"Lines[3].Product\" required value=\"B\"", html);
        Assert.Contains("<template data-wl-repeater-template>", html);
        Assert.Contains("name=\"Lines[__index__].Product\"", html);

        var empty = await RenderAsync<OrderForm>();
        Assert.Contains("name=\"Lines[0].Product\"", empty); // the minimum is pre-rendered
    }

    [Fact]
    public async Task Renders_one_wizard_step_at_a_time()
    {
        var first = await RenderAsync<WizardForm>();
        Assert.Contains("name=\"Email\"", first);
        Assert.DoesNotContain("name=\"DisplayName\"", first);
        Assert.Contains("aria-current=\"step\">Account</li>", first);
        Assert.Contains(">Next</button>", first);
        Assert.DoesNotContain("__wl_wizard_back", first);

        var second = await RenderAsync<WizardForm>(step: 1);
        Assert.Contains("name=\"DisplayName\"", second);
        Assert.DoesNotContain("name=\"Email\"", second);
        Assert.Contains("name=\"__wl_wizard_back\" value=\"1\" formnovalidate>Back</button>", second);
        Assert.Contains(">Submit</button>", second);
    }

    [Fact]
    public async Task File_fields_switch_to_multipart()
    {
        var html = await RenderAsync<UploadForm>(bootstrap: true);

        Assert.Contains("enctype=\"multipart/form-data\"", html);
        Assert.Contains("type=\"file\" class=\"form-control\" id=\"wl-form-upload-Avatar\" name=\"Avatar\" accept=\".png,.jpg\">", html);
        Assert.Contains("name=\"Attachments\" multiple accept=\".txt,.pdf\"", html);
        Assert.Contains("class=\"mb-3\" data-wl-field=\"Avatar\"", html);
    }

    [GeneratedRegex("<script type=\"application/json\" data-wl-form-schema>(.*?)</script>", RegexOptions.Singleline)]
    private static partial Regex SchemaJson();
}
