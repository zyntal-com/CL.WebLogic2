# Forms

A form is a class. Its DataAnnotations are the rules, `[WebField]` and friends are the presentation,
and the same definition drives the markup, the browser validation, the server validation and the
binding back to the model.

## Defining a form

```csharp
[WebForm(Id = "event-signup", Title = "Conference sign-up", SubmitLabel = "Sign me up")]
public sealed class EventSignupForm
{
    [Required, StringLength(80)]
    [WebField(Label = "Full name", Autocomplete = "name")]
    public string? Name { get; set; }

    [Required, WebOptions(Provider = "countries")]
    public string? Country { get; set; }

    [WebShowWhen(nameof(Country), "other"), StringLength(60)]
    [WebField(Label = "Which country?")]
    public string? OtherCountry { get; set; }

    [WebOptions(Provider = "tracks")]
    [WebField(WebFieldType.CheckboxList, Label = "Interesting tracks")]
    public List<string> Tracks { get; set; } = [];

    [WebRepeater(Min = 0, Max = 3)]
    [WebField(Label = "Guests")]
    public List<GuestEntry> Guests { get; set; } = [];

    [WebFile(Extensions = ".png,.jpg,.webp", MaxBytes = 2 * 1024 * 1024, ImagesOnly = true, MaxWidth = 4096, MaxHeight = 4096)]
    public WebUploadedFile? Photo { get; set; }

    [WebField(WebFieldType.Checkbox, Label = "I accept the code of conduct")]
    public bool AcceptConduct { get; set; }
}
```

Register it, plus any options providers and cross-field validators:

```csharp
web.AddForm<EventSignupForm>()
   .AddFormOptions("countries", [new WebFieldOption("dk", "Denmark"), new WebFieldOption("se", "Sweden")])
   .AddFormOptions("tracks", [new WebFieldOption("web", "Web"), new WebFieldOption("security", "Security")])
   .AddFormValidator<EventSignupForm, EventSignupValidator>();
```

Plugins and contributors use `b.MapForm<T>()` / `b.MapFormOptions(provider)`; their forms are removed
with them, and a plugin cannot replace a form the application registered.

**Field types:** Text, TextArea, Email, Password, Number, Range, Date, Time, DateTimeLocal, Color, Tel,
Url, Hidden, Checkbox, CheckboxList, Radio, Select, MultiSelect, Autocomplete, RichText, File. The type
is inferred from the property type and attributes when not given.

**Rules:** `Required`, `StringLength`, `Range`, `EmailAddress`, `Url`, `Compare`, `RegularExpression`
(with a timeout), plus `IWebFormValidator<T>` for cross-field and async rules (e.g. "name taken").
Messages come from the localizer (`IWebTextLocalizer`, keys `forms.required`, `forms.maxLength`, ...; see [Localization](localization.md)).

## Rendering

In a template: `{form:event-signup}` (optional `action="/path"`, `class="..."`, `submit="..."`,
`renderer="plain"`). In code: `await request.Forms.RenderAsync<EventSignupForm>(...)`.

The renderer writes labels, help text, ARIA attributes, native validation attributes, the CSRF field,
the anti-bot fields and a JSON schema the client uses for the same rules. `forms.renderer` selects
`bootstrap5` (default) or `plain`; add your own with `web.AddFormRenderer<T>()`. A theme can override
one field type with `forms/fields/{type}.html`.

## Handling the post

```csharp
b.MapPage("/signup", async (r, ct) =>
{
    var submission = await r.Forms.BindAsync<EventSignupForm>(ct);
    if (!submission.IsValid)
        return submission.Invalid(fallbackTemplate: "templates/signup");   // 422; JSON for the client
    await signups.SaveAsync(submission.Model, ct);
    return FormResult.Success("Thanks!", "/signup/done");               // JSON for the client, 303 otherwise
}, "POST").AllowAnonymous().WithRateLimit("forms");
```

- Posted by the client (`data-wl-form`): errors appear next to the fields without a reload;
  `FormResult` can carry a message, a redirect, data and client commands.
- Posted without the client (plain `<form method="post">` or no JavaScript): an invalid form re-renders
  `fallbackTemplate` with the values and errors; success redirects with `303` and the message is shown on the next
  page as a toast (a one-shot flash). An invalid form without `fallbackTemplate` goes back to the page it was posted
  from (a Referer on this site) with the errors as a toast, otherwise it gets a plain `422` text answer.
- Flash messages work for any redirect: call `WebFlash.Set(http, "Saved.", "success")` (inject `WebFlash`) and the
  next page that renders `{weblogic_scripts}` shows it once. `WebFlash.Take(http)` reads and clears it if a page
  wants to show it itself (for visitors without JavaScript).
- `submission.AddError(field, message)` adds server-side errors (empty field = form-level).
- `request.Forms.Prefill(model)` fills an edit form before rendering.
- Posted names match field names case-insensitively (`email`, `Email` and `EMAIL` all bind to `Email`),
  so clients that post lowercase or camelCase names need no mapping; files included. When a name is
  posted twice in different case, a single-value field takes the first value. Only the model's fields
  bind (`[WebIgnore]` properties never do), and a model whose fields differ only in case is refused
  when the form is registered.

## Mapping to entities

`WebFormMapper` (`request.Forms.Mapper`, or inject it) copies values between a form model and an
entity, so a form does not have to be the entity:

```csharp
// Create: a new entity from the form's fields.
var post = r.Forms.Mapper.ToEntity<PostForm, Post>(submission.Model);

// Update: only the form's fields are written; everything else on the entity stays as loaded.
r.Forms.Mapper.ToEntity(submission.Model, existingPost);

// Edit form: fill the form from the entity, then prefill and render it.
r.Forms.Prefill(r.Forms.Mapper.ToForm<Post, PostForm>(existingPost));
```

- Properties pair up by name (case-insensitive). `[WebFormMapFrom("Body")]` on a form property names
  the entity property; on an entity property it names the form property.
- **Form → entity writes only the form's fields** — what binding fills. `[WebIgnore]` properties,
  properties that are not fields, uploads (`WebUploadedFile`; store them through `IWebUploadSink`),
  read-only and `init`-only entity properties are never written.
- Mark sensitive entity properties `[WebFormMapIgnore]` (owner id, roles, password hash, security
  stamp): they are skipped in both directions, even when a form has a field of the same name.
- Conversions are safe or skipped: nullable wrappers, enums by name (and defined numeric values),
  numbers without overflow or a lost fraction, text to and from numbers, enums, `Guid`, `DateOnly`,
  `DateTime` and the other date/time types in invariant culture (ISO 8601), `DateTime` ↔ `DateOnly`, and
  collections of those (copied, never shared). A value that cannot be converted leaves the target
  unchanged. Null or empty text clears a nullable target and leaves a non-nullable one alone.
- Nested objects and repeaters are copied only when the types are assignable; map their entries
  yourself when the shapes differ. Two form properties mapping to the same entity property throw.
- Property maps are cached per type pair (weakly, so plugin types can unload).

## Options and lookups

`IWebFormOptionsProvider` supplies values for selects, radios, checkbox lists and autocomplete
(`GetOptionsAsync`, `SearchAsync`, `IsValidAsync`). Posted values are always checked against the
provider. Providers are private by default: set `AllowAnonymous` or a `Policy`. The client calls
`/_weblogic/forms/options/{provider}` (rate-limited). `WebFormHttpLookupProvider` proxies an external
HTTPS lookup, only to hosts listed in `forms.allowedLookupHosts`.

Remote validation of a single field (`[WebField(Remote = true)]`) is served at
`/_weblogic/forms/{form}/validate/{field}`; fields without `Remote` are not reachable there.

## Uploads

`WebUploadedFile` properties are bound from multipart posts with size, count and extension limits,
magic-byte detection (`DetectedType`) and image dimensions read by WebLogic's own header reader (PNG,
JPEG, GIF, WebP, BMP). Nothing is stored automatically; call an `IWebUploadSink` (`web.UseUploadSink<T>()`,
default on CL.Storage) or handle the stream yourself. Stored names are random; the original name is
only metadata.

## Wizards

```csharp
public sealed class ProjectWizardForm
{
    [WebStep("basics", Title = "Basics", Order = 1), Required]
    public string? ProjectName { get; set; }

    [WebStep("team", Title = "Team", Order = 2), Range(1, 50)]
    public int TeamSize { get; set; }
}

var submission = await r.Forms.BindAsync<ProjectWizardForm>(ct);
if (!submission.IsComplete || !submission.IsValid)
    return submission.Continue("templates/wizard");    // next step, or the same step with errors
```

Earlier steps travel in a DataProtection-protected token (no server state), each post only accepts
the current step's fields, and the last step validates everything again.

## Anti-bot

On by default per form (`[WebForm(AntiBot = false)]` to turn off, e.g. for sign-in so password
managers can submit at once): a honeypot field and a protected timestamp that rejects posts faster
than `forms.minSubmitSeconds` or older than `forms.maxFormAgeMinutes`. Rejections publish
`FormBotRejectedEvent`.
