using Identity.Domain.Common;

namespace Identity.UnitTests.Domain;

public sealed class MarketTests
{
    [Theory]
    [InlineData("+91 98765 43210")]
    [InlineData("+91-98765-43210")]
    [InlineData("98765 43210")]
    [InlineData("09876543210")]
    [InlineData("919876543210")]
    public void India_accepts_the_formats_the_app_sends(string raw) =>
        Assert.Equal("+919876543210", Market.India.NormalizeMobile(raw));

    [Theory]
    [InlineData("12345")]
    [InlineData("5876543210")]
    [InlineData("+27 71 234 5678")]
    [InlineData("")]
    public void India_rejects_other_numbers(string raw) => Assert.Null(Market.India.NormalizeMobile(raw));

    [Fact]
    public void South_Africa_is_configuration_only() =>
        Assert.Equal("+27712345678", Market.SouthAfrica.NormalizeMobile("071 234 5678"));

    [Fact]
    public void Formats_numbers_like_the_mock()
    {
        Assert.Equal("+91 98765 43210", Market.India.FormatMobile("+919876543210"));
        Assert.Equal("+27 71 234 5678", Market.SouthAfrica.FormatMobile("+27712345678"));
    }

    [Fact]
    public void Masks_claim_numbers_like_the_design()
    {
        Assert.Equal("+91 98•••• 4521", Market.India.MaskMobile("+919876544521"));
        Assert.Equal("+27 82••• 4521", Market.SouthAfrica.MaskMobile("+27824444521"));
    }

    [Fact]
    public void Consent_records_cite_the_market_law()
    {
        Assert.Equal("DPDP", Market.For("IN").PrivacyLaw);
        Assert.Equal("POPIA", Market.For("za").PrivacyLaw);
    }

    [Fact]
    public void Email_is_normalised_and_masked()
    {
        Assert.Equal("yash.p@gmail.com", EmailAddress.Normalize("  Yash.P@Gmail.com "));
        Assert.Null(EmailAddress.Normalize("not-an-email"));
        Assert.Equal("ya•••@gmail.com", EmailAddress.Mask("yash.p@gmail.com"));
    }
}
