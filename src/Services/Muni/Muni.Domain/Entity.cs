namespace Muni.Domain;

/// <summary>Every row has a UUID key and belongs to one tenant (CLAUDE.md §5, §6).</summary>
public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();

    public string TenantId { get; protected set; } = string.Empty;
}
