using System.ComponentModel.DataAnnotations;
using CL.WebLogic.Forms.Attributes;
using CL.WebLogic.Forms.Services;
using CL.WebLogic.Forms.Uploads;

namespace CL.WebLogic.Tests.Forms;

public sealed class WebFormMapperTests
{
    private readonly WebFormMapper _mapper = new();

    public enum Status { Draft, Published }

    public enum OtherStatus { Draft, Published, Archived }

    public sealed class PostForm
    {
        [Required, StringLength(80)]
        public string? Title { get; set; }

        [WebFormMapFrom(nameof(Post.Body))]
        public string? Text { get; set; }

        public string? Status { get; set; }

        public int? Views { get; set; }

        public string? PublishedOn { get; set; }

        public string? Key { get; set; }

        public decimal Rating { get; set; }

        public List<string> Tags { get; set; } = [];

        public string? OwnerId { get; set; } // posted by an attacker: the entity marks OwnerId [WebFormMapIgnore]

        [WebIgnore]
        public string? Slug { get; set; } // not a field: never written to the entity

        public WebUploadedFile? Cover { get; set; }
    }

    public sealed class Post
    {
        public string Title { get; set; } = "";

        public string? Body { get; set; }

        public Status Status { get; set; }

        public long Views { get; set; }

        public DateOnly? PublishedOn { get; set; }

        public Guid Key { get; set; }

        public int Rating { get; set; }

        public string[] Tags { get; set; } = [];

        [WebFormMapIgnore]
        public string OwnerId { get; set; } = "owner-1";

        public string Slug { get; set; } = "original-slug";

        public string? Cover { get; set; } = "keep.png";

        public string Untouched { get; set; } = "untouched";

        public string Computed => Title + "!";

        public string Fixed { get; init; } = "fixed";
    }

    [Fact]
    public void ToEntity_CopiesFields_WithConversions()
    {
        var key = Guid.NewGuid();
        var post = _mapper.ToEntity(new PostForm
        {
            Title = "Hello",
            Text = "Body text",
            Status = "published",
            Views = 42,
            PublishedOn = "2026-10-30",
            Key = key.ToString(),
            Rating = 4m,
            Tags = ["a", "b"]
        }, new Post());

        Assert.Equal("Hello", post.Title);
        Assert.Equal("Body text", post.Body); // renamed with [WebFormMapFrom]
        Assert.Equal(Status.Published, post.Status);
        Assert.Equal(42L, post.Views);
        Assert.Equal(new DateOnly(2026, 10, 30), post.PublishedOn);
        Assert.Equal(key, post.Key);
        Assert.Equal(4, post.Rating);
        Assert.Equal(["a", "b"], post.Tags);
    }

    [Fact]
    public void ToEntity_NeverWrites_IgnoredOrUndefinedProperties()
    {
        var post = new Post();
        _mapper.ToEntity(new PostForm { Title = "x", OwnerId = "attacker", Slug = "evil", Cover = null }, post);

        Assert.Equal("owner-1", post.OwnerId);       // [WebFormMapIgnore] on the entity
        Assert.Equal("original-slug", post.Slug);    // [WebIgnore] on the form: not a field
        Assert.Equal("keep.png", post.Cover);        // uploads are never mapped
        Assert.Equal("untouched", post.Untouched);   // the form has no such field
        Assert.Equal("fixed", post.Fixed);           // init-only
    }

    [Fact]
    public void ToEntity_UnsafeConversions_LeaveTheValue()
    {
        var post = new Post { Status = Status.Draft, Rating = 3, Key = Guid.Empty, Views = 7 };
        _mapper.ToEntity(new PostForm { Title = "x", Status = "Deleted", Rating = 4.5m, Key = "not-a-guid", Views = null }, post);

        Assert.Equal(Status.Draft, post.Status); // undefined enum name
        Assert.Equal(3, post.Rating);            // 4.5 would lose its fraction
        Assert.Equal(Guid.Empty, post.Key);
        Assert.Equal(7, post.Views);             // null cannot clear a non-nullable value
    }

    [Fact]
    public void ToEntity_Nulls_ClearNullableTargets_Only()
    {
        var post = new Post { Title = "keep", Body = "old", PublishedOn = new DateOnly(2020, 1, 1) };
        _mapper.ToEntity(new PostForm { Title = null, Text = null, PublishedOn = "" }, post);

        Assert.Equal("keep", post.Title); // non-nullable string
        Assert.Null(post.Body);
        Assert.Null(post.PublishedOn);    // empty text clears a nullable date
    }

    [Fact]
    public void ToForm_FillsAnEditForm()
    {
        var key = Guid.NewGuid();
        var form = _mapper.ToForm<Post, PostForm>(new Post
        {
            Title = "Hello",
            Body = "Body",
            Status = Status.Published,
            Views = 9,
            PublishedOn = new DateOnly(2026, 1, 2),
            Key = key,
            Rating = 5,
            Tags = ["x"],
            OwnerId = "owner-9",
            Slug = "hello"
        });

        Assert.Equal("Hello", form.Title);
        Assert.Equal("Body", form.Text);
        Assert.Equal("Published", form.Status);
        Assert.Equal(9, form.Views);
        Assert.Equal("2026-01-02", form.PublishedOn);
        Assert.Equal(key.ToString("D"), form.Key);
        Assert.Equal(5m, form.Rating);
        Assert.Equal(["x"], form.Tags);
        Assert.Null(form.OwnerId); // ignored on the entity: never read either
        Assert.Equal("hello", form.Slug); // display-only properties may be filled
        Assert.Null(form.Cover);
    }

    [Fact]
    public void Collections_AreCopied_NotShared()
    {
        var form = new PostForm { Title = "x", Tags = ["a"] };
        var post = _mapper.ToEntity(form, new Post());
        form.Tags.Add("b");

        Assert.Equal(["a"], post.Tags);
    }

    public sealed class AliasEntity
    {
        [WebFormMapFrom("Title")]
        public string? Headline { get; set; }

        public string? Title { get; set; } = "not this one";
    }

    public sealed class TitleForm
    {
        public string? Title { get; set; }
    }

    [Fact]
    public void EntitySideAlias_WinsOverTheSameName()
    {
        var entity = _mapper.ToEntity(new TitleForm { Title = "Hi" }, new AliasEntity());

        Assert.Equal("Hi", entity.Headline);
        Assert.Equal("not this one", entity.Title);
        Assert.Equal("Hi", _mapper.ToForm<AliasEntity, TitleForm>(entity).Title);
    }

    public sealed class NumberForm
    {
        public long Count { get; set; }

        public double Ratio { get; set; }

        public int Level { get; set; }

        public DateTime When { get; set; }
    }

    public sealed class NumberEntity
    {
        public byte Count { get; set; } = 1;

        public float Ratio { get; set; }

        public OtherStatus Level { get; set; }

        public DateOnly When { get; set; }
    }

    [Fact]
    public void Numbers_Enums_AndDates_ConvertSafely()
    {
        var entity = _mapper.ToEntity(new NumberForm { Count = 1000, Ratio = 0.5, Level = 2, When = new DateTime(2026, 5, 6, 7, 8, 9) }, new NumberEntity());

        Assert.Equal(1, entity.Count); // 1000 overflows a byte
        Assert.Equal(0.5f, entity.Ratio);
        Assert.Equal(OtherStatus.Archived, entity.Level);
        Assert.Equal(new DateOnly(2026, 5, 6), entity.When);

        _mapper.ToEntity(new NumberForm { Count = 5, Level = 99 }, entity);
        Assert.Equal(5, entity.Count);
        Assert.Equal(OtherStatus.Archived, entity.Level); // 99 is not defined
    }

    public sealed class ClashForm
    {
        public string? Name { get; set; }

        [WebFormMapFrom("Name")]
        public string? Other { get; set; }
    }

    public sealed class Named
    {
        public string? Name { get; set; }
    }

    [Fact]
    public void TwoFormProperties_ForOneEntityProperty_AreRefused() =>
        Assert.Throws<InvalidOperationException>(() => _mapper.ToEntity(new ClashForm(), new Named()));
}
