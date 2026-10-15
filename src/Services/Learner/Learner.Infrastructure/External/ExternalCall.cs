namespace Learner.Infrastructure.External;

internal static class ExternalCall
{
    /// <summary>
    /// True for any failure of an outside call that should read as "dependency down": network errors, bad JSON,
    /// Polly timeouts and open circuits. False when the caller itself cancelled, so that cancellation still propagates.
    /// </summary>
    public static bool Failed(Exception exception, CancellationToken cancellationToken) =>
        !(exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
        && exception is not (OutOfMemoryException or StackOverflowException);
}
