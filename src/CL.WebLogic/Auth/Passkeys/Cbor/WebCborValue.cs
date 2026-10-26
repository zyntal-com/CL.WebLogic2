namespace CL.WebLogic.Auth.Passkeys.Cbor;

/// <summary>
/// A decoded CBOR value (RFC 8949). Accessors throw <see cref="WebCborException"/> when the value
/// has another type, so parsers can treat any shape mismatch as malformed input.
/// </summary>
public sealed class WebCborValue
{
    private readonly long _integer;
    private readonly byte[]? _bytes;
    private readonly string? _text;
    private readonly IReadOnlyList<WebCborValue>? _items;
    private readonly IReadOnlyList<KeyValuePair<WebCborValue, WebCborValue>>? _entries;
    private readonly double _float;

    private WebCborValue(WebCborType type, long integer = 0, byte[]? bytes = null, string? text = null,
        IReadOnlyList<WebCborValue>? items = null, IReadOnlyList<KeyValuePair<WebCborValue, WebCborValue>>? entries = null, double number = 0)
    {
        Type = type;
        _integer = integer;
        _bytes = bytes;
        _text = text;
        _items = items;
        _entries = entries;
        _float = number;
    }

    /// <summary>The kind of value.</summary>
    public WebCborType Type { get; }

    /// <summary>The semantic tag, when the value was tagged (major type 6).</summary>
    public ulong? Tag { get; private init; }

    /// <summary>The integer value.</summary>
    public long Number => Type == WebCborType.Number ? _integer : throw Mismatch(WebCborType.Number);

    /// <summary>The byte string.</summary>
    public byte[] Bytes => Type == WebCborType.ByteString ? _bytes! : throw Mismatch(WebCborType.ByteString);

    /// <summary>The text string.</summary>
    public string Text => Type == WebCborType.TextString ? _text! : throw Mismatch(WebCborType.TextString);

    /// <summary>The array items.</summary>
    public IReadOnlyList<WebCborValue> Items => Type == WebCborType.Array ? _items! : throw Mismatch(WebCborType.Array);

    /// <summary>The map entries in encoded order.</summary>
    public IReadOnlyList<KeyValuePair<WebCborValue, WebCborValue>> Entries => Type == WebCborType.Map ? _entries! : throw Mismatch(WebCborType.Map);

    /// <summary>The boolean value.</summary>
    public bool Boolean => Type == WebCborType.Boolean ? _integer != 0 : throw Mismatch(WebCborType.Boolean);

    /// <summary>The floating-point value.</summary>
    public double Real => Type == WebCborType.Real ? _float : throw Mismatch(WebCborType.Real);

    /// <summary>The map value under a text key, or null.</summary>
    public WebCborValue? Get(string key) =>
        Entries.FirstOrDefault(e => e.Key.Type == WebCborType.TextString && e.Key._text == key).Value;

    /// <summary>The map value under an integer key, or null.</summary>
    public WebCborValue? Get(long key) =>
        Entries.FirstOrDefault(e => e.Key.Type == WebCborType.Number && e.Key._integer == key).Value;

    /// <summary>The map value under a text key; throws when missing.</summary>
    public WebCborValue Require(string key) => Get(key) ?? throw new WebCborException($"Missing CBOR map key '{key}'.");

    /// <summary>The map value under an integer key; throws when missing.</summary>
    public WebCborValue Require(long key) => Get(key) ?? throw new WebCborException($"Missing CBOR map key {key}.");

    internal static WebCborValue FromInteger(long value) => new(WebCborType.Number, integer: value);

    internal static WebCborValue FromBytes(byte[] value) => new(WebCborType.ByteString, bytes: value);

    internal static WebCborValue FromText(string value) => new(WebCborType.TextString, text: value);

    internal static WebCborValue FromArray(IReadOnlyList<WebCborValue> items) => new(WebCborType.Array, items: items);

    internal static WebCborValue FromMap(IReadOnlyList<KeyValuePair<WebCborValue, WebCborValue>> entries) => new(WebCborType.Map, entries: entries);

    internal static WebCborValue FromBoolean(bool value) => new(WebCborType.Boolean, integer: value ? 1 : 0);

    internal static WebCborValue FromFloat(double value) => new(WebCborType.Real, number: value);

    internal static readonly WebCborValue Null = new(WebCborType.Null);

    internal static readonly WebCborValue Undefined = new(WebCborType.Undefined);

    internal WebCborValue WithTag(ulong tag) =>
        new(Type, _integer, _bytes, _text, _items, _entries, _float) { Tag = tag };

    /// <summary>True when both are the same simple key (integer or text), used for duplicate detection.</summary>
    internal bool SameKey(WebCborValue other) =>
        Type == other.Type && Type switch
        {
            WebCborType.Number => _integer == other._integer,
            WebCborType.TextString => string.Equals(_text, other._text, StringComparison.Ordinal),
            WebCborType.ByteString => _bytes.AsSpan().SequenceEqual(other._bytes),
            WebCborType.Boolean or WebCborType.Null or WebCborType.Undefined => _integer == other._integer,
            _ => false
        };

    private WebCborException Mismatch(WebCborType expected) => new($"Expected CBOR {expected}, found {Type}.");
}
