namespace Muni.Application.Responders;

/// <summary>
/// Muni's platform knowledge: the keyword-scored topics of the mock (muni-knowledge.ts, A-282), shipped as seed data.
/// Every answer is about the signed-in learner and explains a feature in plain steps with links to the right screen.
/// Sample data the mock invents (named jobs, Brain Vita percentages) is replaced by neutral copy until the Careers and
/// Learning services can be asked.
/// </summary>
internal static class MuniKnowledge
{
    public sealed record Topic(string Id, IReadOnlyList<string> Keys, Func<LearnerSnapshot, MuniOptions, MuniAnswer> Answer);

    // Keys added to the mock's: "the arena", "streaks", "daily quests". Without them the chips "How does the Arena
    // work?", "How do streaks work?" and "What are daily quests?" score higher on "work" / "quests" and get the wrong answer.
    public static readonly IReadOnlyList<Topic> Topics =
    [
        new("xp", ["xp", "points", "earn", "experience", "how do i get", "level up", "score"], (m, o) => new(
            "xp",
            [
                T($"XP is how Skillmuni keeps score, {m.First} ⚡ You’ve got {m.Xp} XP right now, Level {m.Level} · {m.Rank}{(m.ToNext > 0 ? $", {m.ToNext} XP to the next level" : string.Empty)}."),
                T($"Ways to earn it:\n• Quests: +{o.QuestStepXp} XP for every step, +{o.QuestCompleteXp} XP when a quest is done (an all-optional step pays once you add something to it)\n• Learning: +1 XP for every right answer in a brief’s quiz\n• Career Drivers assessment: +25 XP per island, 100 XP in all (first time)\n• Your first moves on the Lobby: +50 XP bonus\n• Launchpad missions (once each): finish your job profile +50, first job application +100, first Hire Fest slot +100\n• Today’s quests: the XP shown on each, when you clear it\n• Profile: +5 XP for your first photo, +10 XP for your top skills\n• Programme bonus quest: +100 XP\n• Monthly feedback: +5 XP\nXP never expires, and it’s what ranks you in the Arena."),
                L(
                    I("Keep learning", "Short briefs + quick quizzes", "/home", "+1 XP each"),
                    I("See your XP and badges", "My Stats · Rewards dashboard", "/stats")),
            ],
            ["How do levels work?", "XP vs credits", "How does the Arena work?"])),

        new("levels", ["level", "levels", "rank", "rookie", "pathfinder", "trailblazer", "legend", "icon"], (m, o) => new(
            "levels",
            [
                T($"Levels go up as your XP grows. You’re Level {m.Level} · {m.Rank}.\n{string.Join("\n", o.Ladder().Select(l => $"• Level {l.Level} {l.Rank}: from {l.Min} XP"))}"),
                T(m.ToNext > 0
                    ? $"{m.ToNext} XP to go. A couple of quizzes and a quest step get you most of the way 💪"
                    : "You’re at the top of the ladder. Legend behaviour 👑"),
            ],
            ["How do I earn XP?", "How does the Arena work?"])),

        new("currency", ["coin", "coins", "credit", "credits", "wallet", "currency", "money", "spend"], (_, _) => new(
            "currency",
            [
                T("Three things to collect, each with its own job:\n• ⚡ XP: your score. Levels you up and ranks you in the Arena. Never spent.\n• Coins and the rewards store are coming later. For now, everything you do pays XP.\n• 🎓 Credits: earned by finishing courses and certificates. They show your learning on your CV."),
                L(I("Your wallet", "XP and credits in one place", "/stats")),
            ],
            ["How do I earn XP?", "What are daily quests?"])),

        new("quests", ["quest", "quests", "registration", "register", "onboarding", "who dis", "steps", "skip"], (m, o) => new(
            "quests",
            [
                T($"The 8 quests are how you register and build your job profile 🗺️ You’ve finished {m.QuestsDone} of 8.\n• Each quest has 1–3 short steps (+{o.QuestStepXp} XP each) and a badge at the end (+{o.QuestCompleteXp} XP)\n• Everything autosaves; “Save & exit” picks up exactly where you left\n• Optional steps can be skipped and filled later from My Job Profile"),
                L(I("Open your quests", "See which ones are done", "/welcome-back")),
            ],
            ["What’s left in my profile?", "How do I earn XP?"])),

        new("profile", ["job profile", "profile", "cv", "resume", "left", "missing", "complete", "fill", "gaps"], (m, _) =>
        {
            var blocks = new List<MuniBlock>
            {
                T(m.Gaps.Count > 0
                    ? $"Your job profile is {m.ProfilePercent}% done, {m.First}. It’s built from your quests, so filling a gap is one tap away:"
                    : $"Your job profile is 100% done, {m.First} 🎉 Recruiters see everything you shared, and your CV updates itself."),
            };
            if (m.Gaps.Count > 0)
            {
                blocks.Add(new ListBlock(m.Gaps.Select(g => I(g.Quest, $"Left: {g.Left}", g.Url, "Fill")).ToList()));
            }

            blocks.Add(T("Tip: on My Job Profile, “Edit” on any section opens just that section, already filled in, with Save to bring you straight back."));
            return new MuniAnswer("profile", blocks, ["How does job matching work?", "Is my ID safe?"]);
        }),

        new("jobs", ["job", "jobs", "match", "matching", "apply", "application", "hire", "hiring", "work"], (m, _) => new(
            "jobs",
            [
                T($"My Jobs has four tabs: Available, Matching, Invitations and Applied.\n• Matching ranks jobs by your skills, education, languages, where you want to work and your Career Drivers\n• It’s never locked; the fuller your profile (yours is {m.ProfilePercent}%), the sharper the matches\n• Apply in two taps with your Skillmuni CV, then track it in Applied"),
                L(
                    I("Jobs that match you", "My Jobs · Matching", "/jobs/matching"),
                    I("Your applications", "My Jobs · Applied", "/jobs/applied")),
            ],
            ["What’s Hire Fest?", "What’s left in my profile?"])),

        new("fest", ["hire fest", "fest", "career fest", "recruiter", "interview", "slot", "book", "ticket"], (_, _) => new(
            "fest",
            [
                T("Hire Fest is a recruitment day: meet employers, interview on the spot 🎪\n• Pick a fest, choose employers and time slots, and get one ticket for the day\n• You need a job profile at least 60% done to book\n• Reschedule or cancel anytime from My bookings"),
                L(I("Upcoming fests", "Book a slot", "/hire-fest")),
            ],
            ["How does job matching work?", "What’s left in my profile?"])),

        new("assessment", ["assessment", "career drivers", "archetype", "personality", "test", "compass"], (m, _) => new(
            "assessment",
            [
                T(m.Archetype is not null
                    ? $"Your archetype is {m.Archetype}. It’s the mix of what drives you at work, and it powers your job matches."
                    : "Career Drivers is 36 quick this-or-that picks (about 12 minutes). No right answers, just you."),
                T("• Lean left or right on each pair; you can undo\n• At the end you get your archetype, your top drivers and where you’ll thrive\n• It’s saved as you go, so you can pause anytime"),
                L(m.Archetype is not null
                    ? I("See your full results", "Drivers, thrive zones, courses", "/assessments/career/report")
                    : I("Start Career Drivers", "36 picks · 6 min", "/assessments/career")),
            ],
            ["How does job matching work?", "How do I earn XP?"])),

        // Added (not in the mock): the "Assess" tile's "Skill check" chip and the Launchpad sectors dialog's
        // "Which sectors fit my skills?" question otherwise fall through to "I don't have that one yet".
        new("skills", ["skill check", "skill checks", "skill test", "prove my skills", "self rate"], (m, _) => new(
            "skills",
            [
                T($"Skill checks are coming soon, {m.First} 🛠️ For now, rate your own skills: employers see them on your job profile."),
                T("• Add and self-rate your skills in Your Flex Era (quest 4)\n• Pick your top skills on your profile: +10 XP\n• Career Drivers already shows employers what drives you at work"),
                L(
                    I("Your skills", "Your Flex Era · quest 4", "/quests/your-flex-era"),
                    I("Career Drivers", "36 picks · 6 min", "/assessments/career")),
            ],
            ["Career assessment", "What’s left in my profile?"])),

        new("sectors", ["sector", "sectors", "industry", "industries", "launchpad"], (m, _) => new(
            "sectors",
            [
                T($"Sectors group jobs by industry, {m.First} 🧭 The Launchpad shows how many jobs each one has and marks your best fit from your skills, education and Career Drivers."),
                T($"• Search a sector or role on the Launchpad to see its jobs\n• Your best fit gets sharper as your job profile fills up (yours is {m.ProfilePercent}%)"),
                L(
                    I("Explore sectors", "Launchpad · Sectors", "/launchpad"),
                    I("Jobs that match you", "My Jobs · Matching", "/jobs/matching")),
            ],
            ["How does job matching work?", "What’s left in my profile?"])),

        new("arena", ["arena", "the arena", "leaderboard", "top", "crown", "batch", "league", "compete", "friends"], (m, _) => new(
            "arena",
            [
                T($"The Arena ranks learners by XP 🏆\n• Switch between Everyone, My batch and Friends, and This week, This month or All time\n• The weekly board resets every Monday at midnight\n• Top 3 each week move up a league\nYou have {m.Xp} XP in total."),
                new StatsBlock(
                    [new StatItem("Total XP", $"{m.Xp}", "zap"), new StatItem("Level", $"{m.Level}", "trophy")],
                    new LinkDto("Open the Arena", "/arena")),
            ],
            ["How do I earn XP?", "How do streaks work?"])),

        new("streak", ["streak", "streaks", "daily", "every day", "fire", "flame"], (_, _) => new(
            "streak",
            [
                T("A streak counts the days in a row you learn 🔥\n• Finish at least one brief’s quiz a day to keep it alive\n• Streaks unlock streak badges\n• Miss a day and it goes back to 0, so pick a time that works for you"),
            ],
            ["What are daily quests?", "How do badges work?"])),

        new("daily", ["daily quest", "daily quests", "today", "mystery chest", "challenge"], (_, _) => new(
            "daily",
            [
                T("Today’s quests are three small goals that reset every night: finish a Brain-Vita brief, learn for 15 minutes, score 80%+ on a quiz. Each pays the XP shown when you clear it that day. Chest rewards are coming soon 🎁"),
                L(I("Today’s quests", "My Stats · Rewards", "/stats")),
            ],
            ["XP vs credits", "How do streaks work?"])),

        new("badges", ["badge", "badges", "showcase", "pin", "achievement", "unlock"], (_, _) => new(
            "badges",
            [
                T("Badges mark milestones: quests, learning, quizzes, streaks and more 🏅\n• Each quest gives one when you finish it\n• “Closest to unlock” on My Stats shows what you’re near\n• Pin up to 3 to your showcase so they show on your Player ID"),
                L(I("My badges", "All 48, with progress", "/stats/badges")),
            ],
            ["How do I earn XP?", "How do streaks work?"])),

        new("learn", ["learn", "learning", "module", "modules", "knook", "brief", "briefs", "quiz", "course", "brain vita"], (_, _) => new(
            "learn",
            [
                T("Learning lives in Knooks (shelves of modules) on the Lobby 📚\n• Each module has short briefs (about 2 minutes each)\n• Each brief ends with a quick quiz: +1 XP per right answer\n• Retrying a passed quiz is practice: no XP, but it locks the learning in"),
                L(
                    I("Brain Vita", "Pick up where you left off", "/learn/brain-vita"),
                    I("All Skill Knook modules", "Browse everything", "/knooks/skill-knook")),
            ],
            ["How do I earn XP?", "How do streaks work?"])),

        new("id", ["aadhaar", "digilocker", "id", "pan", "passport", "privacy", "safe", "data", "secure"], (_, _) => new(
            "id",
            [
                T("Your ID is safe 🔒\n• We never store your Aadhaar, PAN or licence number, or a copy of the document\n• You link it through DigiLocker (Government of India), and only the last 4 characters stay with us\n• Mobile and email stay masked until you tap the eye, and employers never see your ID"),
                L(I("Private details", "Profile · Edit profile", "/profile")),
            ],
            ["How do I change my mobile?", "What’s left in my profile?"])),

        new("account", ["change", "mobile", "email", "phone", "number", "photo", "picture", "edit", "gender", "birthday", "dob"], (_, _) => new(
            "account",
            [
                T("All in Profile → Edit profile ✏️\n• Mobile or email: type the new one, we send a 6-digit code to it, and it saves once the code matches\n• Gender and date of birth: pick and save\n• Photo: tap the camera on your Player ID card; it shows in the top bar right away"),
                L(I("Edit profile", "Private details", "/profile")),
            ],
            ["Is my ID safe?", "How do I earn XP?"])),

        new("sound", ["sound", "voice", "mute", "audio", "speak", "talk", "quiet"], (_, _) => new(
            "sound",
            [
                T("Sound effects can be turned on or off in Profile → Settings 🔊 It remembers your choice on this device."),
            ],
            ["What can you do?"])),

        new("muni", ["what can you do", "help", "who are you", "muni", "features", "how does this work", "guide"], (m, _) => new(
            "muni",
            [
                T($"I’m Muni, your Skillmuni buddy 🤙 Ask me anything about the platform, {m.First}. I’m best at:\n• Your progress: XP, level, rank, badges\n• Your job profile and what’s left to fill\n• Jobs, matching and Hire Fest\n• Learning, quizzes and streaks\n• Privacy, your ID and account settings"),
            ],
            ["How do I earn XP?", "What’s left in my profile?", "How does job matching work?"])),
    ];

    /// <summary>The best topic for a question, or null when nothing fits (muni-knowledge.ts <c>answer</c>).</summary>
    public static Topic? Match(string question)
    {
        var q = $" {Words(question)} ";
        Topic? best = null;
        var bestScore = 0;
        foreach (var topic in Topics)
        {
            var score = 0;
            foreach (var key in topic.Keys)
            {
                var phrase = key.Contains(' ', StringComparison.Ordinal);
                if (q.Contains($" {key} ", StringComparison.Ordinal) || (phrase && q.Contains(key, StringComparison.Ordinal)))
                {
                    score += key.Split(' ').Length * 2;
                }
            }

            // Plurals and stems: "badges" vs "badge", "matching" vs "match".
            foreach (var key in topic.Keys)
            {
                if (!key.Contains(' ', StringComparison.Ordinal) && key.Length > 3 && q.Contains(key, StringComparison.Ordinal))
                {
                    score += 1;
                }
            }

            if (score > bestScore)
            {
                best = topic;
                bestScore = score;
            }
        }

        return best;
    }

    public static MuniAnswer Greeting(LearnerSnapshot m) => new(
        "greeting",
        [T($"Heyyy {m.First} 👋 You’re Level {m.Level} · {m.Rank} with {m.Xp} XP. Ask me anything about Skillmuni: XP, jobs, your profile, Hire Fest, the Arena…")],
        ["What can you do?", "How do I earn XP?", "What’s left in my profile?"]);

    public static MuniAnswer Fallback(LearnerSnapshot m) => new(
        "fallback",
        [T($"Hmm, I don’t have that one yet, {m.First} 🤔 Try asking it another way, or pick one of these. I know the platform inside out.")],
        ["What can you do?", "How do I earn XP?", "How does job matching work?"]);

    /// <summary>Lower case; anything but letters, numbers and spaces becomes a space; runs of spaces collapse.</summary>
    internal static string Words(string text)
    {
        var chars = text.ToLowerInvariant().Select(c => char.IsLetter(c) || char.IsNumber(c) || char.IsWhiteSpace(c) ? c : ' ').ToArray();
        return string.Join(' ', new string(chars).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    internal static TextBlock T(string text) => new(text);

    internal static ListItem I(string title, string detail, string url, string? tag = null) => new(title, detail, url, tag);

    internal static ListBlock L(params ListItem[] items) => new(items);
}
