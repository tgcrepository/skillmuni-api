using Azure;
using Azure.AI.DocumentIntelligence;
using Azure.Identity;
using Cv.Application;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cv.Infrastructure.Reading;

/// <summary><c>Cv:Reader</c>. <c>Mode</c>: <c>document-intelligence</c> (Azure) or <c>sandbox</c> (Development and Testing).</summary>
public sealed class CvReaderOptions
{
    public const string Section = "Cv:Reader";

    public const string Sandbox = "sandbox";

    public const string DocumentIntelligence = "document-intelligence";

    public string Mode { get; set; } = DocumentIntelligence;

    /// <summary>The Document Intelligence (Azure AI services) endpoint; the app signs in with its managed identity.</summary>
    public string? Endpoint { get; set; }

    public string Model { get; set; } = "prebuilt-read";
}

/// <summary>
/// Azure AI Document Intelligence "prebuilt-read": OCR for photos and scans, text and reading order for PDF and DOCX,
/// plus the detected language. Files go to Azure in the deployment's region and are not kept by the service.
/// </summary>
internal sealed partial class DocumentIntelligenceTextReader : ICvTextReader
{
    private readonly DocumentIntelligenceClient _client;
    private readonly string _model;
    private readonly ILogger<DocumentIntelligenceTextReader> _logger;

    public DocumentIntelligenceTextReader(IOptions<CvReaderOptions> options, ILogger<DocumentIntelligenceTextReader> logger)
    {
        _client = new DocumentIntelligenceClient(new Uri(options.Value.Endpoint!), new DefaultAzureCredential());
        _model = options.Value.Model;
        _logger = logger;
    }

    public async Task<TextRead> ReadAsync(IReadOnlyList<CvPage> pages, string fileName, CancellationToken cancellationToken)
    {
        var lines = new List<string>();
        var confidence = 0.0;
        var words = 0;
        var pageCount = 0;
        string? locale = null;
        try
        {
            foreach (var page in pages)
            {
                var operation = await _client.AnalyzeDocumentAsync(WaitUntil.Completed, _model, await BinaryData.FromStreamAsync(page.Content, cancellationToken), cancellationToken);
                var result = operation.Value;
                foreach (var p in result.Pages)
                {
                    pageCount++;
                    lines.AddRange(p.Lines.Select(l => l.Content));
                    foreach (var word in p.Words)
                    {
                        confidence += word.Confidence;
                        words++;
                    }
                }

                locale ??= result.Languages?.OrderByDescending(l => l.Confidence).FirstOrDefault()?.Locale;
            }
        }
        catch (RequestFailedException e)
        {
            LogFailed(_logger, e.Status, e.ErrorCode);
            return new TextRead(TextReadStatus.Failed, null);
        }

        if (words == 0)
        {
            return new TextRead(TextReadStatus.Unreadable, null);
        }

        var fromPhoto = pages.All(p => p.ContentType.StartsWith("image/", StringComparison.Ordinal));
        return new TextRead(TextReadStatus.Read, new CvText(lines, locale, confidence / words, pageCount, fromPhoto));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Document Intelligence failed {Status} {ErrorCode}")]
    private static partial void LogFailed(ILogger logger, int status, string? errorCode);
}
