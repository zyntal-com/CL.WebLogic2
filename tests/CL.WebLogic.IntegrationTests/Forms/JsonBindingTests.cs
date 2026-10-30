using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text;
using System.Text.Json;
using CL.WebLogic.Results;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Testing.Services;

namespace CL.WebLogic.IntegrationTests.Forms;

public sealed class JsonBindingTests
{
    // The route skips CSRF so the tests can post plain JSON; the binding is what is under test.
    private static Task<WebLogicTestHost> StartAsync(long? bodyLimit = null) =>
        WebLogicTestHost.StartAsync(new WebLogicTestHostOptions
        {
            ConfigureWebLogic = web => web.AddContributor(new DelegateContributor(b =>
            {
                var route = b.MapApi("/api/orders", async (request, ct) =>
                {
                    var binding = await request.BindJsonAsync<CreateOrder>(ct);
                    if (!binding.IsValid)
                        return binding.ToProblem();
                    return WebResult.Json(new { binding.Model!.Name, Lines = binding.Model.Items.Count });
                }, "POST").AllowAnonymous().AllowCrossSitePost();
                if (bodyLimit is { } limit)
                    route.WithBodyLimit(limit);
            }))
        });

    private static Task<HttpResponseMessage> PostAsync(WebLogicTestHost host, string body, string mediaType = "application/json") =>
        host.Client.PostAsync("/api/orders", new StringContent(body, Encoding.UTF8, mediaType));

    private static async Task<JsonElement> ProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }

    [Fact]
    public async Task Valid_body_binds_and_ignores_unknown_properties()
    {
        await using var host = await StartAsync();

        var response = await PostAsync(host, """{"name":"Ann","isAdmin":true,"items":[{"name":"Pen","quantity":2}]}""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("""{"name":"Ann","lines":1}""", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Missing_required_returns_400_with_camel_case_errors()
    {
        await using var host = await StartAsync();

        var response = await PostAsync(host, """{"items":[]}""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ProblemAsync(response);
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.Equal("name is required.", problem.GetProperty("errors").GetProperty("name")[0].GetString());
    }

    [Fact]
    public async Task Nested_item_errors_use_indexed_keys()
    {
        await using var host = await StartAsync();

        var response = await PostAsync(host, """{"name":"Ann","items":[{"name":"Pen","quantity":1},{"name":"","quantity":1}]}""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await ProblemAsync(response)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("items[1].name", out _));
        Assert.False(errors.TryGetProperty("name", out _));
    }

    [Fact]
    public async Task Wrong_value_type_is_a_field_error()
    {
        await using var host = await StartAsync();

        var response = await PostAsync(host, """{"name":"Ann","items":[{"name":"Pen","quantity":"many"}]}""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(JsonDocument.Parse(body).RootElement.GetProperty("errors").TryGetProperty("items[0].quantity", out _));
        Assert.DoesNotContain("System.Int32", body);
    }

    [Theory]
    [InlineData("""{"name":""")]
    [InlineData("not json")]
    [InlineData("")]
    [InlineData("null")]
    public async Task Malformed_or_empty_json_returns_400(string body)
    {
        await using var host = await StartAsync();

        var response = await PostAsync(host, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(400, (await ProblemAsync(response)).GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Wrong_content_type_returns_415()
    {
        await using var host = await StartAsync();

        var response = await PostAsync(host, """{"name":"Ann"}""", "text/plain");

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal(415, (await ProblemAsync(response)).GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Body_over_the_route_limit_returns_413()
    {
        await using var host = await StartAsync(bodyLimit: 64);

        var response = await PostAsync(host, $$"""{"name":"{{new string('x', 200)}}"}""");

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    private sealed class CreateOrder
    {
        [Required, StringLength(100)]
        public string? Name { get; set; }

        public List<CreateOrderLine> Items { get; set; } = [];
    }

    private sealed class CreateOrderLine
    {
        [Required]
        public string? Name { get; set; }

        [Range(1, 1000)]
        public int Quantity { get; set; }
    }
}
