using System.Text.Json;
using Microsoft.Extensions.Logging;
using SkillMuni.Common.Messaging;
using SkillMuni.Common.Storage;

namespace Cv.Application.Services;

/// <summary>MISS-08: every CV file and extract for the learner. Files first, so a retry can still find them.</summary>
public sealed partial class AccountErasedHandler(ICvRepository documents, IBlobStore blobs, ILogger<AccountErasedHandler> logger)
    : IIntegrationEventHandler<AccountErased>
{
    public async Task HandleAsync(AccountErased e, CancellationToken cancellationToken)
    {
        var all = await documents.ListForLearnerAsync(e.AccountId, cancellationToken);
        foreach (var document in all)
        {
            foreach (var name in JsonSerializer.Deserialize<List<string>>(document.BlobNamesJson) ?? [])
            {
                await blobs.DeleteAsync(name, cancellationToken);
            }

            documents.Remove(document);
        }

        LogErased(logger, e.AccountId, all.Count);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "CV data erased {LearnerId} {Documents}")]
    private static partial void LogErased(ILogger logger, Guid learnerId, int documents);
}
