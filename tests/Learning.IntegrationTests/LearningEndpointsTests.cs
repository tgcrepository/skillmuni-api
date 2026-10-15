using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SkillMuni.Messaging.Inbox;
using SkillMuni.Testing;

namespace Learning.IntegrationTests;

[Collection(LearningApiCollection.Name)]
public sealed class LearningEndpointsTests(LearningApiFactory api)
{
    private const string NonVerbal = "/api/v1/learn/modules/brain-vita/topics/non-verbal-reasoning";

    [Fact]
    public async Task No_session_is_401()
    {
        var response = await api.CreateClient().GetAsync("/api/v1/learn/modules/brain-vita");
        Assert.Equal("no-session", await response.ProblemCodeAsync(HttpStatusCode.Unauthorized));
    }

    [Fact]
    public async Task Module_lists_topics_progress_and_the_resume_point()
    {
        var client = api.ClientFor(out _);

        var module = await (await client.GetAsync("/api/v1/learn/modules/brain-vita")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal("Brain Vita", module.GetProperty("title").GetString());
        Assert.Equal("skill", module.GetProperty("knook").GetProperty("slug").GetString());
        Assert.Equal(105, module.GetProperty("quizzesTotal").GetInt32());
        Assert.Equal(4, module.GetProperty("topics").GetArrayLength());
        Assert.Equal("assets/learn/topic-verbal.svg", module.GetProperty("topics")[0].GetProperty("art").GetString());
        Assert.Equal("brain-vita~verbal-reasoning~0", module.GetProperty("resume").GetProperty("brief").GetProperty("id").GetString());

        Assert.Equal("module-not-found", await (await client.GetAsync("/api/v1/learn/modules/nope")).ProblemCodeAsync(HttpStatusCode.NotFound));
        Assert.Equal("topic-not-found", await (await client.GetAsync("/api/v1/learn/modules/brain-vita/topics/nope")).ProblemCodeAsync(HttpStatusCode.NotFound));
    }

    [Fact]
    public async Task Topic_matches_the_mock_shape()
    {
        var client = api.ClientFor(out _);

        var topic = await (await client.GetAsync(NonVerbal)).JsonAsync(HttpStatusCode.OK);
        var first = topic.GetProperty("briefs")[0];
        Assert.Equal("brain-vita~non-verbal-reasoning~0", first.GetProperty("id").GetString());
        Assert.Equal("current", first.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, first.GetProperty("helpful").ValueKind);
        Assert.False(first.TryGetProperty("example", out _));
        Assert.False(first.TryGetProperty("media", out _));
        Assert.Equal(JsonValueKind.Null, first.GetProperty("quiz").GetProperty("result").ValueKind);

        var mirror = topic.GetProperty("briefs")[10];
        Assert.Equal("Lateral inversion", mirror.GetProperty("keyTerm").GetString());
        Assert.Equal("assets/learn/brief-mirror.svg", mirror.GetProperty("media").GetProperty("src").GetString());
        Assert.False(mirror.GetProperty("media").TryGetProperty("poster", out _));
        Assert.False(topic.GetProperty("course").GetProperty("unlocked").GetBoolean());
        Assert.Equal("Skill Knook", topic.GetProperty("knook").GetProperty("title").GetString());
    }

    [Fact]
    public async Task Bookmark_and_feedback_are_saved()
    {
        var client = api.ClientFor(out _);
        const string brief = "/api/v1/learn/briefs/brain-vita~non-verbal-reasoning~2";

        Assert.Equal(HttpStatusCode.NoContent, (await client.PutJsonAsync($"{brief}/bookmark", new { on = true })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutJsonAsync($"{brief}/feedback", new { helpful = true })).StatusCode);
        var briefs = (await (await client.GetAsync(NonVerbal)).JsonAsync(HttpStatusCode.OK)).GetProperty("briefs");
        Assert.True(briefs[2].GetProperty("bookmarked").GetBoolean());
        Assert.True(briefs[2].GetProperty("helpful").GetBoolean());

        Assert.Equal(HttpStatusCode.NoContent, (await client.PutJsonAsync($"{brief}/feedback", new { helpful = (bool?)null })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutJsonAsync($"{brief}/bookmark", new { on = false })).StatusCode);
        briefs = (await (await client.GetAsync(NonVerbal)).JsonAsync(HttpStatusCode.OK)).GetProperty("briefs");
        Assert.False(briefs[2].GetProperty("bookmarked").GetBoolean());
        Assert.Equal(JsonValueKind.Null, briefs[2].GetProperty("helpful").ValueKind);

        Assert.Equal("brief-not-found", await (await client.PutJsonAsync("/api/v1/learn/briefs/nope/bookmark", new { on = true })).ProblemCodeAsync(HttpStatusCode.NotFound));
        Assert.Equal("validation", await (await client.PutJsonAsync($"{brief}/bookmark", new { })).ProblemCodeAsync(HttpStatusCode.UnprocessableEntity));
    }

    [Fact]
    public async Task Quiz_draft_submit_and_retake()
    {
        var client = api.ClientFor(out var learnerId);

        var quiz = await (await client.GetAsync($"{NonVerbal}/quiz")).JsonAsync(HttpStatusCode.OK);
        var id = quiz.GetProperty("id").GetString()!;
        Assert.Equal("brain-vita~non-verbal-reasoning~0", id);
        Assert.Equal(3, quiz.GetProperty("questions").GetArrayLength());
        Assert.False(quiz.GetProperty("alreadyPassed").GetBoolean());
        Assert.Empty(quiz.GetProperty("draft").EnumerateObject());

        Assert.Equal(HttpStatusCode.NoContent, (await client.PutJsonAsync($"/api/v1/learn/quizzes/{id}/draft", new { answers = new Dictionary<string, string> { ["nv-q1"] = "nv-q1-o1" } })).StatusCode);
        quiz = await (await client.GetAsync($"{NonVerbal}/quiz")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal("nv-q1-o1", quiz.GetProperty("draft").GetProperty("nv-q1").GetString());

        var answers = new Dictionary<string, string> { ["nv-q1"] = "nv-q1-o1", ["nv-q2"] = "nv-q2-o0", ["nv-q3"] = "nv-q3-o1" };
        var result = await (await client.PostJsonAsync($"/api/v1/learn/quizzes/{id}/submit", new { answers })).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(2, result.GetProperty("correct").GetInt32());
        Assert.Equal(3, result.GetProperty("total").GetInt32());
        Assert.Equal(2, result.GetProperty("xp").GetInt32());
        Assert.Equal("Nearly! Water flips it upside down.", result.GetProperty("muni").GetString());
        Assert.Equal("That’s a mirror image. Water reflects from below.", result.GetProperty("results")[1].GetProperty("pickNote").GetString());
        Assert.False(result.GetProperty("results")[0].TryGetProperty("pickNote", out _));

        var retake = await (await client.PostJsonAsync($"/api/v1/learn/quizzes/{id}/submit", new { answers })).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(0, retake.GetProperty("xp").GetInt32());

        var events = await api.QueryAsync(db => db.OutboxMessages.Where(m => m.Payload.Contains(learnerId.ToString())).OrderBy(m => m.OccurredAt).Select(m => m.EventType).ToListAsync());
        Assert.Equal(2, events.Count(e => e == "learning.quiz.submitted"));
        Assert.Single(events, "learning.brief.completed");

        var topic = await (await client.GetAsync(NonVerbal)).JsonAsync(HttpStatusCode.OK);
        Assert.Equal("done", topic.GetProperty("briefs")[0].GetProperty("status").GetString());
        Assert.Equal(2, topic.GetProperty("briefs")[0].GetProperty("xp").GetInt32());
        Assert.Equal(2, topic.GetProperty("xp").GetInt32());
        Assert.Empty((await (await client.GetAsync($"{NonVerbal}/quiz")).JsonAsync(HttpStatusCode.OK)).GetProperty("draft").EnumerateObject());
    }

    [Fact]
    public async Task Submit_errors_and_idempotency_key_replay()
    {
        var client = api.ClientFor(out _);
        var answers = new { answers = new Dictionary<string, string> { ["data-q1"] = "data-q1-o1" } };

        Assert.Equal("quiz-locked", await (await client.PostJsonAsync("/api/v1/learn/quizzes/brain-vita~data-interpretation~3/submit", answers)).ProblemCodeAsync(HttpStatusCode.UnprocessableEntity));
        Assert.Equal("quiz-not-found", await (await client.PostJsonAsync("/api/v1/learn/quizzes/nope/submit", answers)).ProblemCodeAsync(HttpStatusCode.NotFound));
        Assert.Equal("validation", await (await client.PostJsonAsync("/api/v1/learn/quizzes/brain-vita~data-interpretation~0/submit", new { })).ProblemCodeAsync(HttpStatusCode.UnprocessableEntity));

        async Task<HttpResponseMessage> SubmitAsync(string key)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/learn/quizzes/brain-vita~data-interpretation~0/submit") { Content = JsonContent.Create(answers) };
            request.Headers.Add("Idempotency-Key", key);
            return await client.SendAsync(request);
        }

        var first = await SubmitAsync("quiz-1");
        Assert.Equal(1, (await first.JsonAsync(HttpStatusCode.OK)).GetProperty("xp").GetInt32());
        var replay = await SubmitAsync("quiz-1");
        Assert.Equal(1, (await replay.JsonAsync(HttpStatusCode.OK)).GetProperty("xp").GetInt32());
        Assert.Equal("true", replay.Headers.GetValues("Idempotent-Replayed").Single());
    }

    [Fact]
    public async Task Activity_records_minutes()
    {
        var client = api.ClientFor(out var learnerId);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostJsonAsync("/api/v1/learn/activity", new { minutes = 15 })).StatusCode);
        Assert.Equal("validation", await (await client.PostJsonAsync("/api/v1/learn/activity", new { minutes = 0 })).ProblemCodeAsync(HttpStatusCode.UnprocessableEntity));
        Assert.Equal("validation", await (await client.PostJsonAsync("/api/v1/learn/activity", new { minutes = 61 })).ProblemCodeAsync(HttpStatusCode.UnprocessableEntity));

        var payload = await api.QueryAsync(db => db.OutboxMessages.Where(m => m.EventType == "learning.activity.recorded" && m.Payload.Contains(learnerId.ToString())).Select(m => m.Payload).SingleAsync());
        Assert.Equal(15, JsonDocument.Parse(payload).RootElement.GetProperty("minutes").GetInt32());
    }

    [Fact]
    public async Task Lobby_first_moves_end_to_end()
    {
        var client = api.ClientFor(out var learnerId);

        var feed = await (await client.GetAsync("/api/v1/home")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(JsonValueKind.Null, feed.GetProperty("resume").ValueKind);
        Assert.Equal(3, feed.GetProperty("firstSteps").GetArrayLength());
        Assert.Equal(50, feed.GetProperty("firstMovesBonus").GetProperty("xp").GetInt32());
        Assert.Equal(5, feed.GetProperty("knooks").GetArrayLength());
        Assert.Contains(feed.GetProperty("daypart").GetString(), new[] { "morning", "afternoon", "evening" });

        Assert.Equal("first-moves-not-done", await (await client.PostJsonAsync("/api/v1/home/first-moves/claim", new { })).ProblemCodeAsync(HttpStatusCode.Conflict));
        Assert.Equal("step-not-found", await (await client.PostJsonAsync("/api/v1/home/first-steps/nope", new { })).ProblemCodeAsync(HttpStatusCode.NotFound));
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostJsonAsync("/api/v1/home/first-steps/jobs", new { })).StatusCode);
        Assert.Equal(DispatchOutcome.Handled, await api.DeliverAsync("assessment.attempt.submitted", new { learnerId, attempt = 1, firstAttempt = true, tenantId = TestTokens.Tenant }));
        await client.PostJsonAsync("/api/v1/learn/quizzes/brain-vita~verbal-reasoning~0/submit", new { answers = new Dictionary<string, string> { ["verbal-q1"] = "verbal-q1-o1" } });

        feed = await (await client.GetAsync("/api/v1/home")).JsonAsync(HttpStatusCode.OK);
        Assert.All(feed.GetProperty("firstSteps").EnumerateArray(), s => Assert.True(s.GetProperty("done").GetBoolean()));
        Assert.Equal("/learn/brain-vita/verbal-reasoning", feed.GetProperty("resume").GetProperty("link").GetString());
        Assert.Equal(2, feed.GetProperty("resume").GetProperty("brief").GetInt32());

        Assert.Equal(50, (await (await client.PostJsonAsync("/api/v1/home/first-moves/claim", new { })).JsonAsync(HttpStatusCode.OK)).GetProperty("xp").GetInt32());
        Assert.Equal(0, (await (await client.PostJsonAsync("/api/v1/home/first-moves/claim", new { })).JsonAsync(HttpStatusCode.OK)).GetProperty("xp").GetInt32());
        feed = await (await client.GetAsync("/api/v1/home")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(JsonValueKind.Null, feed.GetProperty("firstSteps").ValueKind);
        Assert.Equal(JsonValueKind.Null, feed.GetProperty("firstMovesBonus").ValueKind);

        var claimed = await api.QueryAsync(db => db.OutboxMessages.CountAsync(m => m.EventType == "learning.first-moves.claimed" && m.Payload.Contains(learnerId.ToString())));
        Assert.Equal(1, claimed);
    }

    [Fact]
    public async Task Stats_learning_lists_modules()
    {
        var client = api.ClientFor(out _);
        await client.PostJsonAsync("/api/v1/learn/quizzes/brain-vita~data-interpretation~0/submit", new { answers = new Dictionary<string, string> { ["data-q1"] = "data-q1-o1" } });

        var stats = await (await client.GetAsync("/api/v1/stats/learning")).JsonAsync(HttpStatusCode.OK);
        var top = stats.GetProperty("courses")[0];
        Assert.Equal("brain-vita", top.GetProperty("id").GetString());
        Assert.Equal("in-progress", top.GetProperty("status").GetString());
        Assert.Equal(0.95, top.GetProperty("percent").GetDouble());
        Assert.Equal("Standard", top.GetProperty("kind").GetString());
        Assert.Equal(JsonValueKind.Null, top.GetProperty("deadlineDays").ValueKind);
        Assert.Equal("brain-vita", stats.GetProperty("currentId").GetString());
        Assert.Equal(JsonValueKind.Null, stats.GetProperty("nudge").ValueKind);
    }

    [Fact]
    public async Task Erasure_deletes_the_learners_rows_once()
    {
        var client = api.ClientFor(out var learnerId);
        await client.PutJsonAsync("/api/v1/learn/briefs/brain-vita~non-verbal-reasoning~0/bookmark", new { on = true });
        await client.PostJsonAsync("/api/v1/home/first-steps/jobs", new { });

        var eventId = Guid.NewGuid();
        Assert.Equal(DispatchOutcome.Handled, await api.DeliverAsync("identity.learner.erased", new { accountId = learnerId, tenantId = TestTokens.Tenant }, eventId));
        Assert.Equal(DispatchOutcome.Duplicate, await api.DeliverAsync("identity.learner.erased", new { accountId = learnerId, tenantId = TestTokens.Tenant }, eventId));

        Assert.Equal(0, await api.QueryAsync(db => db.BriefProgress.CountAsync(p => p.LearnerId == learnerId)));
        Assert.Equal(0, await api.QueryAsync(db => db.LobbyStates.CountAsync(p => p.LearnerId == learnerId)));
    }
}
