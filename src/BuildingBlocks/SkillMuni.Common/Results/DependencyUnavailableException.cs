namespace SkillMuni.Common.Results;

/// <summary>
/// A dependency the request can't do without (Service Bus, a provider) is down. The global exception handler answers
/// 503 with <see cref="Code"/> and <see cref="UserMessage"/>; the learner's input is kept and they can retry.
/// </summary>
public sealed class DependencyUnavailableException(string code, string userMessage, Exception? innerException = null)
    : Exception(userMessage, innerException)
{
    public string Code { get; } = code;

    public string UserMessage { get; } = userMessage;
}
