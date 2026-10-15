using System.Net;
using System.Text.Json;
using Learner.Application.Services;
using Learner.Infrastructure.External;
using Learner.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace Learner.UnitTests.Infrastructure;

public sealed class InfrastructureTests
{
    [Theory]
    [InlineData("8.8.8.8", true)]
    [InlineData("2606:4700:4700::1111", true)]
    [InlineData("10.1.2.3", false)]
    [InlineData("172.16.0.1", false)]
    [InlineData("192.168.1.1", false)]
    [InlineData("127.0.0.1", false)]
    [InlineData("169.254.169.254", false)]
    [InlineData("100.64.0.1", false)]
    [InlineData("0.0.0.0", false)]
    [InlineData("::1", false)]
    [InlineData("fe80::1", false)]
    [InlineData("fd00::1", false)]
    [InlineData("::ffff:10.0.0.1", false)]
    [InlineData("224.0.0.1", false)]
    public void Link_checker_refuses_private_and_metadata_addresses(string address, bool isPublic) =>
        Assert.Equal(isPublic, PublicLinkChecker.IsPublic(IPAddress.Parse(address)));

    [Fact]
    public void Field_encryption_round_trips_and_never_repeats_ciphertext()
    {
        var key = Convert.ToBase64String(new byte[32]);
        var encryptor = new AesGcmFieldEncryptor(Options.Create(new LearnerSecurityOptions { EncryptionKey = key }));

        var a = encryptor.Encrypt("50100234567890");
        var b = encryptor.Encrypt("50100234567890");

        Assert.StartsWith("v1:", a, StringComparison.Ordinal);
        Assert.NotEqual(a, b);
        Assert.Equal("50100234567890", encryptor.Decrypt(a));
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("c2hvcnQ=", false)]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=", true)]
    public void Encryption_key_must_be_32_bytes(string key, bool valid) =>
        Assert.Equal(valid, LearnerSecurityOptions.IsValidKey(key));

    [Theory]
    [InlineData("S1.2.3", "people")]
    [InlineData("S2.1", "data")]
    [InlineData("S4.8.1", "business")]
    [InlineData("S5.6", "tech")]
    [InlineData("S7.1", "trades")]
    [InlineData("K0613", "tech")]
    [InlineData("K0211", "creative")]
    [InlineData("K0912", "people")]
    [InlineData("T2", "people")]
    [InlineData(null, "business")]
    public void Esco_groups_map_to_deck_categories(string? code, string category) =>
        Assert.Equal(category, EscoClient.Category(code));

    [Fact]
    public void Esco_results_keep_uri_label_type_and_category()
    {
        const string body = """
        {
          "total": 2,
          "_embedded": { "results": [
            { "uri": "http://data.europa.eu/esco/skill/abc", "title": "use spreadsheets software",
              "preferredLabel": { "en": "use spreadsheets software", "hi": "स्प्रेडशीट" },
              "hasSkillType": ["http://data.europa.eu/esco/skill-type/skill"],
              "broaderHierarchyConcept": ["http://data.europa.eu/esco/skill/S5.6.1"] },
            { "uri": "http://data.europa.eu/esco/skill/def", "title": "accounting",
              "hasSkillType": ["http://data.europa.eu/esco/skill-type/knowledge"],
              "broaderHierarchyConcept": ["http://data.europa.eu/esco/isced-f/0411", "http://data.europa.eu/esco/skill/K0411"] },
            { "title": "no uri, skipped" }
          ] }
        }
        """;
        var result = EscoClient.Map(JsonDocument.Parse(body).RootElement, "hi");

        Assert.Equal(2, result.Total);
        Assert.Collection(
            result.Results,
            s => Assert.Equal(("स्प्रेडशीट", "skill", "tech"), (s.Label, s.Type, s.Category)),
            s => Assert.Equal(("accounting", "knowledge", "business"), (s.Label, s.Type, s.Category)));
    }

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "image/jpeg")]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, "image/png")]
    [InlineData(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31 }, "application/pdf")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50 }, "image/webp")]
    [InlineData(new byte[] { 0, 0, 0, 0x18, 0x66, 0x74, 0x79, 0x70, 0x68, 0x65, 0x69, 0x63 }, "image/heic")]
    [InlineData(new byte[] { 0x4D, 0x5A, 0x90, 0x00 }, null)]
    public void Files_are_typed_by_magic_bytes(byte[] head, string? type) =>
        Assert.Equal(type, FileSniffer.Detect(head));
}
