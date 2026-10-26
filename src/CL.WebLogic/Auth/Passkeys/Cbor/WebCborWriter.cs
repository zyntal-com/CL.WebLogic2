using System.Text;

namespace CL.WebLogic.Auth.Passkeys.Cbor;

/// <summary>
/// Writes CBOR with definite lengths and the shortest argument encodings (CTAP2 canonical form
/// when maps are written in canonical key order). Used for test authenticators and COSE keys.
/// </summary>
public sealed class WebCborWriter
{
    private readonly List<byte> _buffer = [];

    /// <summary>Writes an integer.</summary>
    public WebCborWriter Number(long value) =>
        value >= 0 ? Head(0, (ulong)value) : Head(1, (ulong)(-1 - value));

    /// <summary>Writes a byte string.</summary>
    public WebCborWriter Bytes(ReadOnlySpan<byte> value)
    {
        Head(2, (ulong)value.Length);
        foreach (var b in value)
            _buffer.Add(b);
        return this;
    }

    /// <summary>Writes a text string.</summary>
    public WebCborWriter Text(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? throw new ArgumentNullException(nameof(value)));
        Head(3, (ulong)bytes.Length);
        _buffer.AddRange(bytes);
        return this;
    }

    /// <summary>Starts an array of <paramref name="count"/> items (write them next).</summary>
    public WebCborWriter Array(int count) => Head(4, (ulong)count);

    /// <summary>Starts a map of <paramref name="count"/> entries (write key, value, key, value… next).</summary>
    public WebCborWriter Map(int count) => Head(5, (ulong)count);

    /// <summary>Writes a boolean.</summary>
    public WebCborWriter Boolean(bool value)
    {
        _buffer.Add(value ? (byte)0xF5 : (byte)0xF4);
        return this;
    }

    /// <summary>Writes null.</summary>
    public WebCborWriter Null()
    {
        _buffer.Add(0xF6);
        return this;
    }

    /// <summary>Appends already-encoded CBOR.</summary>
    public WebCborWriter Raw(ReadOnlySpan<byte> encoded)
    {
        foreach (var b in encoded)
            _buffer.Add(b);
        return this;
    }

    /// <summary>The encoded bytes.</summary>
    public byte[] ToArray() => [.. _buffer];

    private WebCborWriter Head(int major, ulong argument)
    {
        var type = (byte)(major << 5);
        if (argument < 24)
            _buffer.Add((byte)(type | (byte)argument));
        else if (argument <= byte.MaxValue)
            _buffer.AddRange([(byte)(type | 24), (byte)argument]);
        else if (argument <= ushort.MaxValue)
            _buffer.AddRange([(byte)(type | 25), (byte)(argument >> 8), (byte)argument]);
        else if (argument <= uint.MaxValue)
            _buffer.AddRange([(byte)(type | 26), (byte)(argument >> 24), (byte)(argument >> 16), (byte)(argument >> 8), (byte)argument]);
        else
        {
            _buffer.Add((byte)(type | 27));
            for (var shift = 56; shift >= 0; shift -= 8)
                _buffer.Add((byte)(argument >> shift));
        }
        return this;
    }
}
