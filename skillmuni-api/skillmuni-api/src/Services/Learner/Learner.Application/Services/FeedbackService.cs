using System.Text.Json;
using Learner.Application.Abstractions;
using Learner.Application.Contracts;
using Learner.Application.Events;
using Learner.Domain;
using Microsoft.Extensions.Options;
using SkillMuni.Common.Messaging;
using SkillMuni.Common.Tenancy;

namespace Learner.Application.Services;

/// <summary>API-040 / A-274: in-app feedback; +5 XP at most once every 30 days.</summary>
public interface IFeedbackService
{
    Task<FeedbackResultDto> SendAsync(LearnerRef who, FeedbackRequest request, CancellationToken cancellationToken);
}

public sealed class FeedbackService(
    ILearnerAccessor accessor,
    IFeedbackRepository feedback,
    IOutbox outbox,
    IUnitOfWork unitOfWork,
    ITenantContext tenant,
    TimeProvider clock,
    IOptions<LearnerServiceOptions> options) : IFeedbackService
{
    public async Task<FeedbackResultDto> SendAsync(LearnerRef who, FeedbackRequest request, CancellationToken cancellationToken)
    {
        var learner = await accessor.GetOrCreateAsync(who, cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        var last = await feedback.LastXpAwardAsync(who.Id, cancellationToken);
        var earnsXp = last is null || now - last.Value >= options.Value.FeedbackXpEvery;

        feedback.Add(Feedback.Give(
            tenant.TenantId,
            who.Id,
            request.Mood,
            JsonSerializer.Serialize(request.Tags ?? []),
            request.Comment?.Trim() ?? string.Empty,
            request.Screen?.Trim() ?? string.Empty,
            earnsXp,
            now));
        outbox.Add(new FeedbackSent(who.Id, request.Mood, earnsXp) { TenantId = tenant.TenantId });

        var xp = earnsXp ? options.Value.Xp.Feedback : 0;
        learner.RecordAwardedXp(xp, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new FeedbackResultDto(xp);
    }
}
