using Learner.Application.Abstractions;
using Learner.Application.Contracts;
using Learner.Application.Events;
using Learner.UnitTests.Fakes;

namespace Learner.UnitTests.Services;

public sealed class ProfileAndTrophyTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, 0x49, 0x48, 0x44, 0x52];
    private static readonly byte[] Pdf = "%PDF-1.7\n%âãÏÓ\n"u8.ToArray();

    private readonly LearnerHarness _h = new();
    private readonly CancellationToken _ct = CancellationToken.None;

    private static UploadedFile Upload(byte[] bytes, string name = "photo.png", string type = "image/png") =>
        new(name, type, bytes.Length, new MemoryStream(bytes));

    [Fact]
    public async Task New_profile_has_contact_and_name_tasks_open_and_masks_nothing_it_does_not_have()
    {
        var who = LearnerHarness.NewLearner();
        var profile = await _h.Profile.GetAsync(who, _ct);

        Assert.Equal("Asha", profile.Name);
        Assert.Equal(0, profile.Strength.Percent);
        Assert.Equal("Rookie", profile.Title);
        Assert.Null(profile.PhotoUrl);
        Assert.All(profile.PrivateFields, f => Assert.True(f.Empty));
        Assert.StartsWith("SM-", profile.PlayerId, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verified_contacts_are_masked_and_revealed_on_request()
    {
        var who = LearnerHarness.NewLearner();
        await _h.Accessor.GetOrCreateAsync(who, _ct);
        _h.Learner(who).SetVerifiedContact("+919876540697", "asha.rao@example.com", DateTime.UtcNow);

        var profile = await _h.Profile.GetAsync(who, _ct);
        var mobile = profile.PrivateFields.Single(f => f.Key == "mobile");
        Assert.Equal("•••••• 0697", mobile.Masked);
        Assert.Equal("a••••@example.com", profile.PrivateFields.Single(f => f.Key == "email").Masked);
        Assert.Equal("+91 98765 40697", (await _h.Profile.RevealAsync(who, "mobile", _ct)).Value);
        Assert.Equal(30, profile.Strength.Percent);
    }

    [Fact]
    public async Task First_skills_earn_xp_once_and_are_capped_at_five()
    {
        var who = LearnerHarness.NewLearner();
        var edit = new ProfileEditRequest(["Sales", "sales", "Excel", "Hindi", "English", "Teamwork", "Leadership"], "female", "2004-08-21");

        var first = (await _h.Profile.SaveAsync(who, edit, _ct)).Value;
        var again = (await _h.Profile.SaveAsync(who, edit, _ct)).Value;

        Assert.Equal(10, first.XpEarned);
        Assert.Equal(0, again.XpEarned);
        Assert.Equal(new[] { "Sales", "Excel", "Hindi", "English", "Teamwork" }, first.Profile.Skills);
        Assert.Equal("Female", (await _h.Profile.RevealAsync(who, "gender", _ct)).Value);
        Assert.Equal("21 Aug 2004", (await _h.Profile.RevealAsync(who, "dob", _ct)).Value);
        Assert.Single(_h.Store.EventsOf<ProfileTaskCompleted>(), e => e.Task == "skills");
    }

    [Fact]
    public async Task A_birthday_in_the_future_is_refused()
    {
        var result = await _h.Profile.SaveAsync(LearnerHarness.NewLearner(), new ProfileEditRequest([], "rather-not", "2030-01-01"), _ct);
        Assert.Equal("dob-in-future", result.Error!.Code);
    }

    [Fact]
    public async Task First_photo_earns_xp_and_a_new_one_replaces_the_old_blob()
    {
        var who = LearnerHarness.NewLearner();

        var first = (await _h.Profile.UploadPhotoAsync(who, Upload(Png), _ct)).Value;
        var oldBlob = _h.Learner(who).PhotoBlob!;
        var second = (await _h.Profile.UploadPhotoAsync(who, Upload(Png), _ct)).Value;

        Assert.Equal(5, first.XpEarned);
        Assert.Equal(0, second.XpEarned);
        Assert.Equal("/api/v1/profile/photo?v=2", second.Profile.PhotoUrl);
        Assert.Contains(oldBlob, _h.Blobs.Deleted);
        Assert.Single(_h.Blobs.Blobs);
        Assert.Equal(2, _h.Store.EventsOf<PhotoChanged>().Count());

        var photo = (await _h.Profile.GetPhotoAsync(who.Id, _ct)).Value;
        Assert.Equal("image/png", photo.ContentType);
    }

    [Fact]
    public async Task A_file_is_typed_by_its_bytes_not_its_name()
    {
        var who = LearnerHarness.NewLearner();
        var renamed = Upload(Pdf, "photo.png", "image/png");

        Assert.Equal("unsupported-type", (await _h.Profile.UploadPhotoAsync(who, renamed, _ct)).Error!.Code);
        Assert.Equal("file-missing", (await _h.Profile.UploadPhotoAsync(who, null, _ct)).Error!.Code);
        _h.Settings.MaxPhotoBytes = 4;
        Assert.Equal("too-large", (await _h.Profile.UploadPhotoAsync(who, Upload(Png), _ct)).Error!.Code);
    }

    [Fact]
    public async Task Certificates_accept_pdf_and_keep_only_a_safe_file_name()
    {
        var who = LearnerHarness.NewLearner();
        var result = await _h.Trophies.UploadAsync(who, Upload(Pdf, "C:\\Users\\asha\\nptel.pdf", "application/pdf"), _ct);

        Assert.True(result.IsSuccess);
        var stored = Assert.Single(_h.Store.Trophies);
        Assert.Equal("nptel.pdf", stored.FileName);
        Assert.Equal("application/pdf", stored.ContentType);
        Assert.Equal(stored.Id.ToString(), result.Value.FileId);
    }

    [Fact]
    public async Task Link_check_looks_for_the_learners_names_and_maps_a_timeout_to_504()
    {
        var who = LearnerHarness.NewLearner();
        var request = new CheckLinkRequest("https://www.credly.com/badges/abc");

        Assert.True((await _h.Trophies.CheckLinkAsync(who, request, _ct)).Value.Verified);
        Assert.Equal(new[] { "Asha" }, _h.Links.Names!);

        _h.Links.Next = Lookup<bool>.Timeout();
        Assert.Equal("link-timeout", (await _h.Trophies.CheckLinkAsync(who, request, _ct)).Error!.Code);

        _h.Links.Next = Lookup<bool>.Unavailable();
        Assert.False((await _h.Trophies.CheckLinkAsync(who, request, _ct)).Value.Verified);
    }

    [Fact]
    public async Task DigiLocker_is_india_only_and_503_when_unavailable()
    {
        Assert.Equal("market-not-supported", (await _h.Trophies.ImportDigiLockerAsync(LearnerHarness.NewLearner(market: "ZA"), _ct)).Error!.Code);
        _h.DigiLocker.IsAvailable = false;
        Assert.Equal("digilocker-unavailable", (await _h.Trophies.ImportDigiLockerAsync(LearnerHarness.NewLearner(), _ct)).Error!.Code);
    }

    [Fact]
    public async Task Linking_an_id_keeps_the_masked_number_sets_the_birthday_and_shows_on_the_profile()
    {
        var who = LearnerHarness.NewLearner();

        var linked = (await _h.IdentityLinks.LinkAsync(who, new LinkDigiLockerRequest("aadhaar", "code"), _ct)).Value;
        var profile = await _h.Profile.GetAsync(who, _ct);

        Assert.Equal("XXXX XXXX 4821", linked.Masked);
        Assert.Equal("2004-08-21", linked.Birthday);
        Assert.Equal(new DateOnly(2004, 8, 21), _h.Learner(who).DateOfBirth);
        Assert.Equal("Aadhaar XXXX XXXX 4821", profile.PrivateFields.Single(f => f.Key == "id").Masked);

        _h.DigiLocker.Next = Lookup<DigiLockerDocument>.Found(new DigiLockerDocument("XXXXXX234F", "234F", null, null));
        await _h.IdentityLinks.LinkAsync(who, new LinkDigiLockerRequest("pan", "code"), _ct);
        Assert.Equal("pan", Assert.Single(_h.Store.Identities).Document);

        await _h.IdentityLinks.UnlinkAsync(who.Id, _ct);
        Assert.Null(await _h.IdentityLinks.CurrentAsync(who.Id, _ct));
    }

    [Fact]
    public async Task A_rejected_digilocker_code_is_422()
    {
        _h.DigiLocker.Next = Lookup<DigiLockerDocument>.NotFound();
        var result = await _h.IdentityLinks.LinkAsync(LearnerHarness.NewLearner(), new LinkDigiLockerRequest("aadhaar", "bad"), _ct);
        Assert.Equal("digilocker-rejected", result.Error!.Code);
    }
}
