using System.Buffers.Binary;
using System.Text;

namespace CL.WebLogic.Auth.Passkeys.Cbor;

/// <summary>
/// A strict CBOR decoder (RFC 8949) for WebAuthn: definite lengths only, no duplicate map keys,
/// integers within <see cref="long"/>, at most 16 levels and 4096 items, and every length checked
/// against the remaining input before anything is allocated. Anything else is a
/// <see cref="WebCborException"/>.
/// </summary>
public static class WebCbor
{
    private const int MaxDepth = 16;
    private const int MaxItems = 4096;

    /// <summary>Decodes one value that must span all of <paramref name="data"/>.</summary>
    public static WebCborValue Decode(ReadOnlySpan<byte> data)
    {
        var value = Decode(data, out var read);
        if (read != data.Length)
            throw new WebCborException($"{data.Length - read} unexpected bytes after the CBOR value.");
        return value;
    }

    /// <summary>Decodes the first value in <paramref name="data"/>; <paramref name="bytesRead"/> says where it ended.</summary>
    public static WebCborValue Decode(ReadOnlySpan<byte> data, out int bytesRead)
    {
        var reader = new Reader(data);
        var value = reader.ReadValue(0);
        bytesRead = reader.Position;
        return value;
    }

    private ref struct Reader(ReadOnlySpan<byte> data)
    {
        private readonly ReadOnlySpan<byte> _data = data;
        private int _items;

        public int Position { get; private set; }

        public WebCborValue ReadValue(int depth)
        {
            if (depth > MaxDepth)
                throw new WebCborException("CBOR nesting is too deep.");
            if (++_items > MaxItems)
                throw new WebCborException("Too many CBOR items.");

            var initial = Take(1)[0];
            var major = initial >> 5;
            var info = initial & 0x1F;

            if (major == 7)
                return ReadSimple(info);

            var argument = ReadArgument(info);
            switch (major)
            {
                case 0:
                    return argument <= long.MaxValue ? WebCborValue.FromInteger((long)argument) : throw new WebCborException("CBOR integer out of range.");
                case 1:
                    return argument <= long.MaxValue ? WebCborValue.FromInteger(-1 - (long)argument) : throw new WebCborException("CBOR integer out of range.");
                case 2:
                    return WebCborValue.FromBytes(Take(Length(argument)).ToArray());
                case 3:
                    try
                    {
                        return WebCborValue.FromText(new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(Take(Length(argument))));
                    }
                    catch (DecoderFallbackException ex)
                    {
                        throw new WebCborException("CBOR text is not valid UTF-8.", ex);
                    }
                case 4:
                {
                    var count = Count(argument, 1);
                    var items = new WebCborValue[count];
                    for (var i = 0; i < count; i++)
                        items[i] = ReadValue(depth + 1);
                    return WebCborValue.FromArray(items);
                }
                case 5:
                {
                    var count = Count(argument, 2);
                    var entries = new List<KeyValuePair<WebCborValue, WebCborValue>>(count);
                    for (var i = 0; i < count; i++)
                    {
                        var key = ReadValue(depth + 1);
                        if (key.Type is WebCborType.Array or WebCborType.Map or WebCborType.Real)
                            throw new WebCborException("Unsupported CBOR map key type.");
                        if (entries.Any(e => e.Key.SameKey(key)))
                            throw new WebCborException("Duplicate CBOR map key.");
                        entries.Add(new(key, ReadValue(depth + 1)));
                    }
                    return WebCborValue.FromMap(entries);
                }
                default: // 6: tag
                    return ReadValue(depth + 1).WithTag(argument);
            }
        }

        private WebCborValue ReadSimple(int info) => info switch
        {
            20 => WebCborValue.FromBoolean(false),
            21 => WebCborValue.FromBoolean(true),
            22 => WebCborValue.Null,
            23 => WebCborValue.Undefined,
            25 => WebCborValue.FromFloat((double)BinaryPrimitives.ReadHalfBigEndian(Take(2))),
            26 => WebCborValue.FromFloat(BinaryPrimitives.ReadSingleBigEndian(Take(4))),
            27 => WebCborValue.FromFloat(BinaryPrimitives.ReadDoubleBigEndian(Take(8))),
            31 => throw new WebCborException("Indefinite-length CBOR is not supported."),
            _ => throw new WebCborException($"Unsupported CBOR simple value {info}.")
        };

        private ulong ReadArgument(int info) => info switch
        {
            < 24 => (ulong)info,
            24 => Take(1)[0],
            25 => BinaryPrimitives.ReadUInt16BigEndian(Take(2)),
            26 => BinaryPrimitives.ReadUInt32BigEndian(Take(4)),
            27 => BinaryPrimitives.ReadUInt64BigEndian(Take(8)),
            31 => throw new WebCborException("Indefinite-length CBOR is not supported."),
            _ => throw new WebCborException($"Reserved CBOR additional information {info}.")
        };

        /// <summary>A byte or text length: must fit in what is left.</summary>
        private readonly int Length(ulong argument) =>
            argument <= (ulong)(_data.Length - Position) ? (int)argument : throw new WebCborException("CBOR length exceeds the input.");

        /// <summary>An item count: each item takes at least one byte, so it must fit too.</summary>
        private readonly int Count(ulong argument, int bytesPerItem) =>
            argument <= (ulong)((_data.Length - Position) / bytesPerItem) && argument <= MaxItems
                ? (int)argument
                : throw new WebCborException("CBOR item count exceeds the input.");

        private ReadOnlySpan<byte> Take(int count)
        {
            if (count > _data.Length - Position)
                throw new WebCborException("Truncated CBOR.");
            var slice = _data.Slice(Position, count);
            Position += count;
            return slice;
        }
    }
}
