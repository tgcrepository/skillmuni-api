using SkillMuni.Common.Privacy;

namespace SkillMuni.Testing;

/// <summary>In-memory erasure tombstones for Application-layer unit tests.</summary>
public sealed class FakeErasureTombstones : IErasureTombstones
{
    public HashSet<Guid> Erased { get; } = [];

    public Task<bool> IsErasedAsync(Guid learnerId, CancellationToken cancellationToken) => Task.FromResult(Erased.Contains(learnerId));

    public Task MarkErasedAsync(Guid learnerId, CancellationToken cancellationToken)
    {
        Erased.Add(learnerId);
        return Task.CompletedTask;
    }
}
