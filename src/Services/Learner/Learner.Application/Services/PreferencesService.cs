using Learner.Application.Abstractions;
using Learner.Application.Contracts;

namespace Learner.Application.Services;

/// <summary>MISS-09: sounds, voice and language, kept on the server so they follow the learner across devices.</summary>
public interface IPreferencesService
{
    Task<PreferencesDto> GetAsync(LearnerRef who, CancellationToken cancellationToken);

    Task<PreferencesDto> SaveAsync(LearnerRef who, PreferencesRequest request, CancellationToken cancellationToken);
}

public sealed class PreferencesService(ILearnerAccessor accessor, IUnitOfWork unitOfWork, TimeProvider clock) : IPreferencesService
{
    public async Task<PreferencesDto> GetAsync(LearnerRef who, CancellationToken cancellationToken)
    {
        var learner = await accessor.GetOrCreateAsync(who, cancellationToken);
        return new PreferencesDto(learner.SoundsOn, learner.VoiceOn, learner.Language);
    }

    public async Task<PreferencesDto> SaveAsync(LearnerRef who, PreferencesRequest request, CancellationToken cancellationToken)
    {
        var learner = await accessor.GetOrCreateAsync(who, cancellationToken);
        learner.SetPreferences(request.Sounds, request.Voice, request.Language, clock.GetUtcNow().UtcDateTime);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new PreferencesDto(learner.SoundsOn, learner.VoiceOn, learner.Language);
    }
}
