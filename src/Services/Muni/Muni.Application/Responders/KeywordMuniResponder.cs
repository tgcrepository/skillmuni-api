using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using static Muni.Application.Responders.MuniKnowledge;

namespace Muni.Application.Responders;

/// <summary>
/// MISS-14 phase 1: the mock's keyword-intent catalogue (muni.handlers.ts <c>reply</c> + muni-knowledge.ts), in the same
/// order: topic tiles, greetings, free questions scored against <see cref="MuniKnowledge"/>, then the fixed chips.
/// </summary>
public sealed partial class KeywordMuniResponder(IPrivateFieldClient privateFields, IOptions<MuniOptions> options) : IMuniResponder
{
    /// <summary>"Or pick a vibe" tiles: the intro line and the chips each one opens.</summary>
    internal static readonly IReadOnlyDictionary<string, (string Intro, string[] Chips)> TopicTiles = new Dictionary<string, (string, string[])>(StringComparer.Ordinal)
    {
        ["jobs"] = ("Jobs it is 💼 What do you want to do?", ["Jobs for me", "Applications", "Career Compass"]),
        ["learn"] = ("Let’s learn something 📚 Pick one:", ["Continue my quest", "Quick quiz", "Browse modules"]),
        ["assess"] = ("Time to flex 🎯 Which one?", ["Career assessment", "Skill check"]),
        ["me"] = ("All about you ✨", ["Show my profile", "My XP & rank", "My career archetype"]),
    };

    public async Task<MuniAnswer> AnswerAsync(MuniQuestion question, LearnerSnapshot me, CancellationToken cancellationToken)
    {
        var q = (string.IsNullOrWhiteSpace(question.Intent) ? question.Text : question.Intent).ToLowerInvariant().Trim();

        if (TopicTiles.TryGetValue(q, out var tile))
        {
            return new MuniAnswer($"tile:{q}", [T(tile.Intro)], tile.Chips);
        }

        // A-282: greetings and platform questions are answered for this learner.
        if (GreetingPattern().IsMatch(q) && q.Split(' ').Length <= 3)
        {
            return MuniKnowledge.Greeting(me);
        }

        if (!Chip().IsMatch(q))
        {
            return Match(q) is { } topic ? topic.Answer(me, options.Value) : Fallback(me);
        }

        if (ProfileChip().IsMatch(q))
        {
            return await ProfileAsync(me, cancellationToken);
        }

        if (XpChip().IsMatch(q))
        {
            return new MuniAnswer(
                "chip:xp",
                [
                    T($"You’re cooking, {me.First} 🔥 Here’s where you stand:"),
                    new StatsBlock(
                        [
                            new StatItem("Total XP", $"{me.Xp}", "zap"),
                            new StatItem("Level", $"{me.Level} · {me.Rank}", "trophy"),
                            new StatItem("To next level", $"{me.ToNext}", "trend-up"),
                        ],
                        new LinkDto("Open the Arena", "/arena")),
                    T("One module and a quiz gets you a good chunk of the way. Want me to pick one?"),
                ],
                ["Continue my quest", "Quick quiz", "Jobs for me"]);
        }

        if (JobsChip().IsMatch(q))
        {
            return new MuniAnswer(
                "chip:jobs",
                [
                    T($"Your matches live on My Jobs 👀 The fuller your profile (yours is {me.ProfilePercent}%), the sharper they get."),
                    L(
                        I("Jobs that match you", "Ranked by your skills, places and Career Drivers", "/jobs/matching"),
                        I("Your applications", "Track every job you applied to", "/jobs/applied")),
                ],
                ["Why do I match?", "Applications", "Career Compass"]);
        }

        if (CareerChip().IsMatch(q))
        {
            return new MuniAnswer(
                "chip:archetype",
                [
                    T(me.Archetype is not null
                        ? $"Your archetype is {me.Archetype} 🤝 It’s what drives you at work, and it shapes your job matches."
                        : "You haven’t taken Career Drivers yet. 36 quick picks (6 min) and I’ll tell you your archetype 🎯"),
                ],
                ["Jobs for me", "Take the Career assessment", "My XP & rank"]);
        }

        if (LearnChip().IsMatch(q))
        {
            return new MuniAnswer(
                "chip:learn",
                [
                    T("Pick up Brain Vita where you left off 🧠 Every right answer in a brief’s quiz is +1 XP."),
                    L(I("Brain Vita", "Resume where you left off", "/learn/brain-vita", "+1 XP each")),
                ],
                ["Browse modules", "My XP & rank", "Jobs for me"]);
        }

        if (q.Contains("streak", StringComparison.Ordinal))
        {
            return new MuniAnswer(
                "chip:streak",
                [
                    T(me.Streak > 0
                        ? $"{me.Streak}-day streak and counting 🔥 One brief’s quiz today keeps it alive."
                        : "No streak yet 🔥 Finish one brief’s quiz today to start one."),
                ],
                ["Quick quiz", "Continue my quest"]);
        }

        if (CvChip().IsMatch(q))
        {
            return new MuniAnswer(
                "chip:cv",
                [T("Got your CV 📄 I’ll compare it with your profile and show you what’s new before anything changes.")],
                ["Show my profile", "Jobs for me"]);
        }

        if (HelloChip().IsMatch(q))
        {
            return new MuniAnswer(
                "chip:hello",
                [T("Heyyy 👋 Ask me about jobs, learning, your rank or your profile.")],
                ["Jobs for me", "Continue my quest", "My XP & rank"]);
        }

        return new MuniAnswer(
            "fallback",
            [T("Hmm, I’m not sure about that one yet 🤔 I’m best with jobs, learning, assessments and your profile.")],
            ["Jobs for me", "Continue my quest", "Show my profile"]);
    }

    internal static string MaskEmail(string email)
    {
        var at = email.IndexOf('@', StringComparison.Ordinal);
        var domain = at >= 0 ? email[(at + 1)..] : string.Empty;
        return $"{(email.Length > 0 ? email[..1] : string.Empty)}••••@{domain}";
    }

    internal static string MaskMobile(string mobile)
    {
        var digits = new string(mobile.Where(char.IsAsciiDigit).ToArray());
        return $"••••••{(digits.Length > 4 ? digits[^4..] : digits)}";
    }

    private async Task<MuniAnswer> ProfileAsync(LearnerSnapshot me, CancellationToken cancellationToken)
    {
        var email = await privateFields.RevealAsync(RevealFields.Email, cancellationToken);
        var mobile = await privateFields.RevealAsync(RevealFields.Mobile, cancellationToken);
        var gap = me.Gaps.Count > 0 ? me.Gaps[0] : null;

        return new MuniAnswer(
            "chip:profile",
            [
                T("Here’s you on Skillmuni 👇 Private bits stay masked. Tap the eye to peek."),
                new ProfileBlock(
                    me.Initials,
                    me.FullName,
                    $"{me.Rank} · Lvl {me.Level}",
                    [
                        Private("email", "Email", email, MaskEmail),
                        Private("mobile", "Mobile", mobile, MaskMobile),
                        new ProfileField("since", "Member since", me.JoinedAt is { } joined ? joined.ToString("MMM yyyy", CultureInfo.InvariantCulture) : "—", false),
                    ],
                    me.ProfilePercent,
                    gap is not null ? $"Next: {gap.Quest} · {gap.Left}" : "All done. Nice!"),
                T("Want the tea on your XP, rank or career archetype? 👀"),
            ],
            ["My XP & rank", "My career archetype", "Jobs for me"]);
    }

    /// <summary>Unavailable → a masked placeholder the eye can retry; none on file → "Not added".</summary>
    private static ProfileField Private(string key, string label, string? value, Func<string, string> mask) => value switch
    {
        null => new ProfileField(key, label, "••••••", true),
        "" => new ProfileField(key, label, "Not added", false),
        _ => new ProfileField(key, label, mask(value), true),
    };

    [GeneratedRegex(@"^(hi|hey|hello|yo|hii+|namaste)\b", RegexOptions.CultureInvariant)]
    private static partial Regex GreetingPattern();

    [GeneratedRegex(@"^(show my profile|my xp & rank|jobs for me|continue my quest|my career archetype|quick quiz|browse modules|applications|career compass|take the career assessment|why do i match\?)$", RegexOptions.CultureInvariant)]
    private static partial Regex Chip();

    [GeneratedRegex("profile|about me|my details", RegexOptions.CultureInvariant)]
    private static partial Regex ProfileChip();

    [GeneratedRegex("xp|rank|leaderboard|arena", RegexOptions.CultureInvariant)]
    private static partial Regex XpChip();

    [GeneratedRegex("job|match|work|hire|application", RegexOptions.CultureInvariant)]
    private static partial Regex JobsChip();

    [GeneratedRegex("archetype|compass|career", RegexOptions.CultureInvariant)]
    private static partial Regex CareerChip();

    [GeneratedRegex("quest|continue|module|learn|brain|quiz|brief", RegexOptions.CultureInvariant)]
    private static partial Regex LearnChip();

    [GeneratedRegex("cv|resume|attach", RegexOptions.CultureInvariant)]
    private static partial Regex CvChip();

    [GeneratedRegex(@"hi|hey|hello|yo\b", RegexOptions.CultureInvariant)]
    private static partial Regex HelloChip();
}
