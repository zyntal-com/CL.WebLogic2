using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using CL.WebLogic.Forms.Services;
using CL.WebLogic.Forms.Validation;
using CL.WebLogic.Runtime.Services;

namespace CL.WebLogic.Tests.Forms;

public sealed class JsonValidatorTests
{
    private static readonly JsonSerializerOptions Options = WebJson.CreateOptions();

    private static WebJsonValidator Validator { get; } = new(FormTestServices.Messages);

    [Fact]
    public void Valid_model_has_no_errors()
    {
        var order = new Order { CustomerName = "Ann", Items = [new OrderLine { Name = "Pen", Quantity = 1 }] };
        Assert.Empty(Validator.Validate(order, Options));
    }

    [Fact]
    public void Keys_are_camel_case_json_names_and_required_uses_the_forms_text()
    {
        var errors = Validator.Validate(new Order(), Options);

        Assert.Equal(["customerName is required."], errors["customerName"]);
        Assert.False(errors.ContainsKey("CustomerName"));
    }

    [Fact]
    public void JsonPropertyName_and_Display_name_are_used()
    {
        var errors = Validator.Validate(new Renamed(), Options);

        Assert.Equal(["Full name is required."], errors["full_name"]);
    }

    [Fact]
    public void Nested_objects_and_collections_get_dotted_and_indexed_keys()
    {
        var order = new Order
        {
            CustomerName = "Ann",
            Address = new OrderAddress { City = "" },
            Items = [new OrderLine { Name = "Pen", Quantity = 1 }, new OrderLine { Name = "", Quantity = 0 }]
        };

        var errors = Validator.Validate(order, Options);

        Assert.True(errors.ContainsKey("address.city"));
        Assert.True(errors.ContainsKey("items[1].name"));
        Assert.True(errors.ContainsKey("items[1].quantity"));
        Assert.False(errors.ContainsKey("items[0].name"));
    }

    [Fact]
    public void Dictionaries_are_walked_by_key()
    {
        var model = new Catalog { Lines = new Dictionary<string, OrderLine> { ["a"] = new() { Name = "", Quantity = 1 } } };

        Assert.True(Validator.Validate(model, Options).ContainsKey("lines[a].name"));
    }

    [Fact]
    public void ValidatableObject_runs_after_members_are_valid_and_maps_member_names()
    {
        var errors = Validator.Validate(new DateRange { From = 5, To = 1 }, Options);
        Assert.Equal(["To must be after from."], errors["to"]);

        var whole = Validator.Validate(new DateRange { From = 1, To = 2, Reject = true }, Options);
        Assert.Equal(["Rejected."], whole[string.Empty]);

        var memberFirst = Validator.Validate(new DateRange { From = -1, To = -5 }, Options);
        Assert.True(memberFirst.ContainsKey("from"));
        Assert.False(memberFirst.ContainsKey("to"));
    }

    [Fact]
    public void ValidatableObject_in_a_collection_is_keyed_by_its_path()
    {
        var model = new Schedule { Ranges = [new DateRange { From = 1, To = 2 }, new DateRange { From = 3, To = 1 }] };

        Assert.Equal(["To must be after from."], Validator.Validate(model, Options)["ranges[1].to"]);
    }

    [Fact]
    public void Depth_guard_stops_deep_nesting()
    {
        var root = new Node();
        var current = root;
        for (var i = 0; i < 10; i++)
            current = current.Child = new Node();

        var errors = Validator.Validate(root, Options, maxDepth: 3);

        var key = Assert.Single(errors).Key;
        Assert.Equal("child.child.child.child", key);
        Assert.Empty(Validator.Validate(root, Options));
    }

    [Fact]
    public void Cycles_do_not_loop()
    {
        var node = new Node();
        node.Child = node;

        Assert.Empty(Validator.Validate(node, Options));
    }

    [Fact]
    public void Ignored_members_are_not_validated()
    {
        Assert.Empty(Validator.Validate(new WithIgnored(), Options));
    }

    [Theory]
    [InlineData("$.name", "name")]
    [InlineData("$.items[0].quantity", "items[0].quantity")]
    [InlineData("$[2].name", "[2].name")]
    [InlineData("$", null)]
    [InlineData("$['a.b']", null)]
    [InlineData(null, null)]
    public void Json_exception_paths_become_field_keys(string? path, string? expected) =>
        Assert.Equal(expected, WebJsonBinder.FieldKey(path));

    private sealed class Order
    {
        [Required, StringLength(50)]
        public string? CustomerName { get; set; }

        public OrderAddress? Address { get; set; }

        public List<OrderLine> Items { get; set; } = [];
    }

    private sealed class OrderAddress
    {
        [Required]
        public string? City { get; set; }
    }

    private sealed class OrderLine
    {
        [Required]
        public string? Name { get; set; }

        [Range(1, 100)]
        public int Quantity { get; set; }
    }

    private sealed class Catalog
    {
        public Dictionary<string, OrderLine> Lines { get; set; } = [];
    }

    private sealed class Renamed
    {
        [Required, Display(Name = "Full name"), JsonPropertyName("full_name")]
        public string? FullName { get; set; }
    }

    private sealed class DateRange : IValidatableObject
    {
        [Range(0, int.MaxValue)]
        public int From { get; set; }

        public int To { get; set; }

        public bool Reject { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (To < From)
                yield return new ValidationResult("To must be after from.", [nameof(To)]);
            if (Reject)
                yield return new ValidationResult("Rejected.");
        }
    }

    private sealed class Schedule
    {
        public List<DateRange> Ranges { get; set; } = [];
    }

    private sealed class Node
    {
        public Node? Child { get; set; }
    }

    private sealed class WithIgnored
    {
        [Required, JsonIgnore]
        public string? Secret { get; set; }
    }
}
