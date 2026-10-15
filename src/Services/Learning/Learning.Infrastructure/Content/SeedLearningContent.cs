using Learning.Application;
using Learning.Domain;

namespace Learning.Infrastructure.Content;

/// <summary>
/// The catalogue as the app's mocks define it (<c>learn.handlers.ts</c> for modules, topics, briefs and quizzes;
/// <c>home.handlers.ts</c> for pathways and Knooks). Brain Vita and Digital Basics carry the design copy; every other
/// module a Lobby tile links to gets the mock's generic content (two topics of three briefs). Static until a CMS exists.
/// </summary>
public sealed class SeedLearningContent : ILearningContent
{
    private const string LearnArt = "assets/learn/";
    private const string HomeArt = "assets/home/";

    private static readonly string[] TopicArt = ["verbal", "non-verbal", "arithmetic", "data"];

    private static readonly KnookRef SkillKnook = new("skill", "Skill Knook");
    private static readonly KnookRef IndustryKnook = new("industry", "Industry Knook");
    private static readonly KnookRef BlueCollarKnook = new("blue-collar", "Blue Collar Knook");
    private static readonly KnookRef PracticeKnook = new("practice", "Practice Knook");
    private static readonly KnookRef CourseKnook = new("course", "Digital Foundations Knook");

    private static readonly string[] Tones = ["arcane", "ember", "green", "amber", "pink", "blue"];

    private readonly Dictionary<string, ModuleContent> _bySlug;

    public SeedLearningContent()
    {
        Knooks = BuildKnooks();
        Modules = BuildModules(Knooks);
        _bySlug = Modules.ToDictionary(m => m.Slug, StringComparer.Ordinal);
        Pathways = BuildPathways();
    }

    public IReadOnlyList<ModuleContent> Modules { get; }

    public IReadOnlyList<Pathway> Pathways { get; }

    public IReadOnlyList<Knook> Knooks { get; }

    public ModuleContent? Module(string slug) => _bySlug.GetValueOrDefault(slug);

    private static string Art(int i) => $"{LearnArt}topic-{TopicArt[i]}.svg";

    private static string Tile(string art) => $"{HomeArt}tile-{art}.png";

    /* ---------- Modules ---------- */

    private static List<ModuleContent> BuildModules(IReadOnlyList<Knook> knooks)
    {
        var modules = new List<ModuleContent> { BrainVita(), DigitalBasics() };
        var known = modules.Select(m => m.Slug).ToHashSet(StringComparer.Ordinal);

        // Every other module a Lobby tile links to: the mock's generic content, titled and shelved as on the Lobby.
        foreach (var knook in knooks)
        {
            var shelf = knook.Slug switch
            {
                "industry" => IndustryKnook,
                "blue-collar" => BlueCollarKnook,
                "practice" => PracticeKnook,
                "course" => CourseKnook,
                _ => SkillKnook,
            };
            foreach (var tile in knook.Modules.Where(t => known.Add(t.Slug)))
            {
                modules.Add(Generic(tile.Slug, tile.Title, shelf, tile.Art, Tones[modules.Count % Tones.Length]));
            }
        }

        return modules;
    }

    private static ModuleContent Generic(string slug, string title, KnookRef knook, string tileArt, string tone) => new(
        slug,
        title,
        $"Short briefs and quick quizzes on {title}.",
        knook,
        tileArt,
        tone,
        new[] { "Getting Started", "Going Deeper" }
            .Select((t, i) => new TopicContent(
                t.ToLowerInvariant().Replace(' ', '-'),
                t,
                Art(i),
                GenericBriefs(3, t),
                GenericQuiz($"{slug}-{i}")))
            .ToList());

    private static List<BriefContent> GenericBriefs(int n, string topic) =>
        Enumerable.Range(1, n)
            .Select(i => new BriefContent(
                $"{topic} · Brief {i}",
                $"Short, focused reading for {topic.ToLowerInvariant()}. Read it once, then take the one-question quiz to lock it in."))
            .ToList();

    private static List<QuizQuestion> GenericQuiz(string topic) =>
    [
        new(
            $"{topic}-q1",
            "What’s the best way to lock in what you just read?",
            ["Skip to the next module", "Try the quiz straight away", "Read it ten times", "Wait a week"],
            1,
            "Answering right after reading (retrieval practice) is what makes it stick."),
    ];

    private static ModuleContent BrainVita()
    {
        string[] nonVerbalTitles =
        [
            "Series Completion", "Analogy", "Classification", "Odd One Out", "Pattern Completion", "Figure Matrix",
            "Rule Detection", "Dot Situation", "Cubes and Dice",
        ];

        var nonVerbal = nonVerbalTitles
            .Select(t => new BriefContent(t, $"{t}: spot the rule that links the figures, then apply it to find the missing one. Work feature by feature and rule options out."))
            .Append(Embedded)
            .Append(Mirror)
            .ToList();

        return new ModuleContent(
            "brain-vita",
            "Brain Vita",
            "Sharpen your reasoning, one quiz at a time.",
            SkillKnook,
            Tile("brain-vita"),
            "arcane",
            [
                new TopicContent("verbal-reasoning", "Verbal Reasoning", Art(0), GenericBriefs(51, "Verbal Reasoning"), GenericQuiz("verbal")),
                new TopicContent("non-verbal-reasoning", "Non-Verbal Reasoning", Art(1), nonVerbal, MirrorQuiz, Course: "Non-Verbal Reasoning: Full course + certificate"),
                new TopicContent("arithmetical-ability", "Arithmetical Ability", Art(2), GenericBriefs(37, "Arithmetical Ability"), GenericQuiz("arith")),
                new TopicContent("data-interpretation", "Data Interpretation", Art(3), GenericBriefs(6, "Data Interpretation"), GenericQuiz("data")),
            ]);
    }

    private static ModuleContent DigitalBasics() => new(
        "digital-basics",
        "Digital Basics",
        "Tools and habits for the modern workplace.",
        CourseKnook,
        Tile("it-gyan"),
        "green",
        [
            new TopicContent(
                "digital-workplace-essentials",
                "Digital Workplace Essentials",
                Art(3),
                GenericBriefs(10, "Digital Workplace"),
                WorkplaceQuiz,
                QuizTitle: "Digital Workplace Essentials"),
        ]);

    private static readonly BriefContent Mirror = new(
        "Mirror and Water Images",
        "A mirror image is how a figure looks when a mirror is placed beside it: left and right swap, while top and bottom stay the same. A water image is the reflection you see in still water below a figure: top and bottom swap, while left and right stay the same. In these questions you pick the correct reflection from four options. Check one feature at a time, such as a corner, a dot or a letter, and track where it lands after the flip. Rule out any option that breaks even one rule.",
        Example: "Hold the word “CAB” up to a mirror on its right and you read BAC, with every letter flipped sideways. Reflect the same word in water and the order stays C-A-B, but every letter turns upside down. An option that keeps the right order but does not flip the letters is wrong.",
        KeyTerm: "Lateral inversion",
        VideoUrl: "https://www.youtube.com/results?search_query=mirror+and+water+images+reasoning",
        Media: new BriefMedia(
            "image",
            $"{LearnArt}brief-mirror.svg",
            "An orange triangle and its mirror image in violet on the other side of a vertical mirror line; the dot moves from the bottom-left corner to the bottom-right."));

    private static readonly BriefContent Embedded = new(
        "Embedded Figures",
        "An embedded figure is a simple shape hidden inside a more complex one. Look for the shape’s most distinctive edge or angle first, then check that every other line of it is present in the same size and orientation.",
        KeyTerm: "Lateral inversion",
        Media: new BriefMedia("image", $"{LearnArt}brief-mirror.svg", "Two triangles either side of a line."));

    private static readonly IReadOnlyList<QuizQuestion> MirrorQuiz =
    [
        new(
            "nv-q1",
            "A mirror is placed to the right of a figure. What changes in its mirror image?",
            ["Top and bottom swap", "Left and right swap", "Nothing changes", "The figure turns 90°"],
            1,
            "A side mirror flips the figure sideways: left and right swap, top and bottom stay where they are.",
            new Dictionary<int, string> { [0] = "Top and bottom swap in a water image, not a mirror image." },
            "So close! A side mirror flips left and right."),
        new(
            "nv-q2",
            "What does a water image do to a figure?",
            ["Left and right swap", "Top and bottom swap, left and right stay", "The figure shrinks", "Both directions swap"],
            1,
            "Still water sits below the figure, so the reflection turns it upside down while keeping left and right.",
            new Dictionary<int, string> { [0] = "That’s a mirror image. Water reflects from below." },
            "Nearly! Water flips it upside down."),
        new(
            "nv-q3",
            "You hold the word “CAB” up to a mirror on its right. What do you read?",
            ["CAB, upside down", "BAC, with every letter flipped sideways", "CAB, unchanged", "BAC, upside down"],
            1,
            "The order reverses and each letter is laterally inverted, which is why it reads BAC with flipped letters.",
            new Dictionary<int, string> { [0] = "Upside down is the water image of CAB." },
            "Almost! Order and letters both flip in a mirror."),
    ];

    private static readonly IReadOnlyList<QuizQuestion> WorkplaceQuiz =
    [
        new(
            "dw-q1",
            "Which tool is best suited for formal correspondence in a digital workplace?",
            ["Social media posts", "Instant messaging", "Video conferencing", "Email"],
            3,
            "Email is the standard for formal messages: it keeps a written record, can carry attachments and is easy to refer back to later.",
            new Dictionary<int, string>
            {
                [1] = "Instant messaging is great for quick, casual chats, but it’s informal and messages get buried fast.",
                [0] = "Social media is public and informal, so it isn’t the place for formal work messages.",
                [2] = "Video calls are for live conversations, not written correspondence.",
            },
            "So close! Email’s the formal one."),
        new(
            "dw-q2",
            "What is a key benefit of using instant messaging in a work setting?",
            [
                "It allows for quick queries and immediate responses",
                "It requires significant setup time",
                "It is ideal for sending very long documents",
                "It replaces all other communication methods",
            ],
            0,
            "Instant messaging is built for short questions that need a fast reply, without waiting for an email or booking a meeting.",
            Muni: "Nearly! IM is all about speed."),
        new(
            "dw-q3",
            "Video conferencing is primarily used for:",
            ["Sending physical mail", "Virtual meetings and face-to-face discussions", "Creating spreadsheets", "Storing files offline"],
            1,
            "Video calls let teams in different places meet face to face in real time, share screens and talk things through.",
            Muni: "Close one! Video calls are for meetings."),
    ];

    /* ---------- Lobby (home.handlers.ts) ---------- */

    private static List<Pathway> BuildPathways() =>
    [
        new(
            "career-launchpad",
            "Career Launchpad",
            "Take assessments to match jobs with your skills and needs. Opportunities are waiting!",
            "arcane",
            $"{HomeArt}pathway-career.png",
            "/launchpad"),
        new(
            "grad-gateway",
            "Grad Gateway",
            "Compare countries, find top institutes and register your interest. We’ll guide you all the way!",
            "ember",
            $"{HomeArt}pathway-grad.png",
            null),
        new(
            "startup-street",
            "Startup Street",
            "Test your Entrepreneurial Quotient, share ideas, find collaborators and explore new paths!",
            "success",
            $"{HomeArt}pathway-startup.png",
            null),
    ];

    private static KnookTile T(string slug, string title, string? art = null) =>
        new(slug, title, Tile(art ?? slug), $"/learn/{slug}");

    private static List<Knook> BuildKnooks()
    {
        string[] blueCollar = ["Electrician Basics", "Safety First", "Plumbing 101", "Retail Floor", "Delivery Pro", "Security Guard"];
        string[] blueCollarArt = ["surgical-strike", "sharp-shooter", "it-gyan", "banking", "hospitality", "insurance"];
        return
        [
            new(
                "skill",
                "Skill Knook",
                $"{HomeArt}knook-skill.png",
                true,
                [
                    T("brain-vita", "Brain Vita"),
                    T("english-vinglish", "English Vinglish"),
                    T("its-my-style", "It’s My Style"),
                    T("happiness-barometer", "Happiness Barometer"),
                    T("sharp-shooter", "Sharp Shooter"),
                    T("surgical-strike", "Surgical Strike"),
                    T("money-smart", "Money Smart", "banking"),
                    T("speak-up", "Speak Up", "english-vinglish"),
                    T("time-boss", "Time Boss", "happiness-barometer"),
                    T("digital-detox", "Digital Detox", "it-gyan"),
                ]),
            new(
                "industry",
                "Industry Knook",
                $"{HomeArt}knook-industry.png",
                true,
                [T("insurance", "Insurance"), T("banking", "Banking"), T("hospitality", "Hospitality"), T("it-gyan", "IT Gyan")]),
            new(
                "blue-collar",
                "Blue Collar Knook",
                $"{HomeArt}knook-blue-collar.png",
                false,
                blueCollar.Select((title, i) => T($"blue-collar-{i + 1}", title, blueCollarArt[i])).ToList()),
            new(
                "practice",
                "Practice Knook",
                $"{HomeArt}knook-practice.png",
                false,
                [T("mock-interviews", "Mock Interviews", "sharp-shooter")]),
            new(
                "course",
                "Course Knook",
                $"{HomeArt}knook-course.png",
                false,
                [T("digital-basics", "Digital Basics", "it-gyan")]),
        ];
    }
}
