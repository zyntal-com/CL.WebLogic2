using CL.WebLogic.Auth.Passkeys.Cbor;

namespace CL.WebLogic.Tests.Auth.Passkeys;

public sealed class CborTests
{
    [Theory]
    [InlineData(0L)]
    [InlineData(23L)]
    [InlineData(24L)]
    [InlineData(255L)]
    [InlineData(256L)]
    [InlineData(65536L)]
    [InlineData(4294967296L)]
    [InlineData(long.MaxValue)]
    [InlineData(-1L)]
    [InlineData(-25L)]
    [InlineData(-257L)]
    [InlineData(long.MinValue)]
    public void Integers_round_trip_with_the_shortest_encoding(long value)
    {
        var encoded = new WebCborWriter().Number(value).ToArray();
        Assert.Equal(value, WebCbor.Decode(encoded).Number);
        var expectedLength = value is >= -24 and < 24 ? 1 : value is >= -256 and < 256 ? 2 : value is >= -65536 and < 65536 ? 3 : value is >= -4294967296 and < 4294967296 ? 5 : 9;
        Assert.Equal(expectedLength, encoded.Length);
    }

    [Fact]
    public void Nested_structures_round_trip()
    {
        var encoded = new WebCborWriter().Map(3)
            .Text("fmt").Text("none")
            .Number(-2).Bytes([1, 2, 3])
            .Text("list").Array(3).Boolean(true).Null().Text("ü")
            .ToArray();

        var map = WebCbor.Decode(encoded);
        Assert.Equal("none", map.Require("fmt").Text);
        Assert.Equal([1, 2, 3], map.Require(-2).Bytes);
        var list = map.Require("list").Items;
        Assert.True(list[0].Boolean);
        Assert.Equal(WebCborType.Null, list[1].Type);
        Assert.Equal("ü", list[2].Text);
        Assert.Null(map.Get("missing"));
        Assert.Throws<WebCborException>(() => map.Require("fmt").Bytes); // wrong type is malformed input
    }

    [Fact]
    public void Floats_and_tags_decode()
    {
        Assert.Equal(1.0, WebCbor.Decode([0xF9, 0x3C, 0x00]).Real);
        Assert.Equal(1.5, WebCbor.Decode([0xFA, 0x3F, 0xC0, 0x00, 0x00]).Real);
        var tagged = WebCbor.Decode([0xC1, 0x1A, 0x51, 0x4B, 0x67, 0xB0]);
        Assert.Equal(1UL, tagged.Tag);
        Assert.Equal(1363896240, tagged.Number);
    }

    [Theory]
    [InlineData("5F")]                       // indefinite byte string
    [InlineData("9F01FF")]                   // indefinite array
    [InlineData("0101")]                     // trailing bytes
    [InlineData("A2616101616102")]           // duplicate map key "a"
    [InlineData("A201010102")]               // duplicate integer key 1
    [InlineData("5A00000010")]               // byte string claims 16 bytes, has none
    [InlineData("5BFFFFFFFFFFFFFFFF")]       // absurd length
    [InlineData("9BFFFFFFFFFFFFFFFF")]       // absurd item count
    [InlineData("1BFFFFFFFFFFFFFFFF")]       // integer beyond long
    [InlineData("3BFFFFFFFFFFFFFFFF")]       // negative integer beyond long
    [InlineData("1C")]                       // reserved additional information
    [InlineData("F8FF")]                     // unsupported simple value
    [InlineData("62C328")]                   // invalid UTF-8
    [InlineData("A1810101")]                 // array as map key
    [InlineData("")]                         // empty
    public void Malformed_or_unsupported_input_is_refused(string hex) =>
        Assert.Throws<WebCborException>(() => WebCbor.Decode(Convert.FromHexString(hex)));

    [Fact]
    public void Nesting_and_item_counts_are_bounded()
    {
        byte[] deep = [.. Enumerable.Repeat((byte)0x81, 17), 0x01];
        Assert.Throws<WebCborException>(() => WebCbor.Decode(deep));

        byte[] shallow = [.. Enumerable.Repeat((byte)0x81, 16), 0x01];
        Assert.Equal(WebCborType.Array, WebCbor.Decode(shallow).Type);

        var many = new WebCborWriter().Array(5000);
        for (var i = 0; i < 5000; i++)
            many.Number(0);
        Assert.Throws<WebCborException>(() => WebCbor.Decode(many.ToArray()));
    }

    [Fact]
    public void Decode_with_length_reports_where_the_value_ends()
    {
        byte[] data = [0x82, 0x01, 0x02, 0xFF, 0xFF];
        var value = WebCbor.Decode(data, out var read);
        Assert.Equal(3, read);
        Assert.Equal(2, value.Items.Count);
    }
}
