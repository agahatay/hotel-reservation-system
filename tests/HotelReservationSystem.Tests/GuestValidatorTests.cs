using HotelReservationSystem.Core.Domain;
using HotelReservationSystem.Core.Models;

namespace HotelReservationSystem.Tests;

public class GuestValidatorTests
{
    private static IReadOnlyList<string> Validate(string first = "Ada", string last = "Lovelace",
        string? phone = null, string? email = null, string? id = null) =>
        GuestValidator.Validate(GuestValidator.Normalize(new Guest
        {
            FirstName = first, LastName = last, Phone = phone, Email = email, NationalId = id
        }));

    [Fact]
    public void Valid_guest_has_no_errors() =>
        Assert.Empty(Validate(phone: "+90 555 010 0101", email: "ada@example.com", id: "P7000108"));

    [Fact]
    public void Optional_fields_may_be_blank() => Assert.Empty(Validate(phone: "  ", email: "", id: null));

    [Theory]
    [InlineData("", "Lovelace")]
    [InlineData("Ada", "   ")]
    public void Names_are_required(string first, string last) => Assert.Single(Validate(first, last));

    [Fact]
    public void Names_longer_than_50_are_rejected() => Assert.Single(Validate(first: new string('a', 51)));

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("a@b")]
    [InlineData("two@@example.com")]
    public void Invalid_email_is_rejected(string email) => Assert.Contains(Validate(email: email), e => e.Contains("Email"));

    [Theory]
    [InlineData("abc-defg")]
    [InlineData("123")]
    [InlineData("+90 555 010 0101 0101 01")]
    public void Invalid_phone_is_rejected(string phone) => Assert.Contains(Validate(phone: phone), e => e.Contains("Phone"));

    [Fact]
    public void National_id_with_symbols_is_rejected() => Assert.NotEmpty(Validate(id: "12-34;DROP"));

    [Fact]
    public void Normalize_trims_and_nulls_blank_fields()
    {
        var g = GuestValidator.Normalize(new Guest { FirstName = "  Ada ", LastName = " Lovelace", Phone = " ", Email = " ada@example.com " });
        Assert.Equal("Ada", g.FirstName);
        Assert.Equal("Lovelace", g.LastName);
        Assert.Null(g.Phone);
        Assert.Equal("ada@example.com", g.Email);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("123", "•••")]
    [InlineData("D10000001", "•••••0001")]
    public void National_id_is_masked_except_last_four(string? value, string expected) =>
        Assert.Equal(expected, Masking.MaskIdentifier(value));
}
