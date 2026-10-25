using System.ComponentModel.DataAnnotations;
using CL.WebLogic.Forms.Attributes;
using CL.WebLogic.Forms.Models;
using CL.WebLogic.Forms.Providers;
using CL.WebLogic.Forms.Services;
using CL.WebLogic.Results;
using CL.WebLogic.Routing.Models;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Testing.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CL.WebLogic.IntegrationTests.Forms;

public sealed class FormContributionTests
{
    [WebForm(Id = "plugin-note")]
    public sealed class PluginNoteForm
    {
        [Required, WebOptions(Provider = "plugin-moods")]
        public string? Mood { get; set; }
    }

    [WebForm(Id = "contact")]
    public sealed class HijackForm
    {
        public string? Name { get; set; }
    }

    [Fact]
    public async Task Plugins_register_forms_and_options_until_removed()
    {
        await using var host = await FormsTestSite.StartAsync();
        host.WriteContentFile("theme/templates/note.html", "{layout:layouts/main}{form:plugin-note}");
        var registrar = host.Services.GetRequiredService<ContributorRegistrar>();
        var plugin = new DelegateContributor(b =>
        {
            b.MapForm<PluginNoteForm>();
            b.MapFormOptions(new StaticOptionsProvider("plugin-moods", [new WebFieldOption("calm", "Calm")]));
            b.MapPage("/note", (_, _) => ValueTask.FromResult<WebResult>(WebResult.Page("templates/note"))).AllowAnonymous();
        });

        await using (await registrar.RegisterAsync(plugin, new WebContributorDescriptor { Id = "plugin.notes", Name = "Notes" }, CancellationToken.None))
        {
            var html = await host.Client.GetStringAsync("/note");
            Assert.Contains("data-wl-form=\"plugin-note\"", html);
            Assert.Contains("<option value=\"calm\">Calm</option>", html);
        }

        var forms = host.Services.GetRequiredService<WebFormRegistry>();
        Assert.Null(forms.Find("plugin-note"));
        Assert.Null(host.Services.GetRequiredService<WebFormOptionsRegistry>().Get("plugin-moods"));
    }

    [Fact]
    public async Task A_plugin_cannot_replace_an_application_form()
    {
        await using var host = await FormsTestSite.StartAsync();
        var registrar = host.Services.GetRequiredService<ContributorRegistrar>();
        var hijack = new DelegateContributor(b => b.MapForm<HijackForm>());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            registrar.RegisterAsync(hijack, new WebContributorDescriptor { Id = "plugin.bad", Name = "Bad" }, CancellationToken.None));
        Assert.Equal(typeof(ContactForm), host.Services.GetRequiredService<WebFormRegistry>().Find("contact"));
    }
}
