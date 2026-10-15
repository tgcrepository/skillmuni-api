using Microsoft.AspNetCore.Mvc;

namespace SkillMuni.Web.Idempotency;

/// <summary>
/// Marks a POST that creates a record or awards XP (CLAUDE.md §4). When the request carries an
/// <c>Idempotency-Key</c> header, a retry with the same key gets the first response back instead of running twice.
/// Requests without the header run normally.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class IdempotentAttribute : TypeFilterAttribute
{
    public IdempotentAttribute()
        : base(typeof(IdempotencyFilter))
    {
    }
}
