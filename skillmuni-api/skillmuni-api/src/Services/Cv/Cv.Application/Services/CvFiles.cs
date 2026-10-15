using System.Text;

namespace Cv.Application.Services;

/// <summary>
/// File types decided from the first bytes (S1). PDF, DOCX and photos are what Document Intelligence reads; legacy
/// .doc and WEBP are refused with a clear message (flagged: the dropzone still lists them).
/// </summary>
internal static class CvFiles
{
    public const string Pdf = "application/pdf";
    public const string Docx = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    public const string Jpeg = "image/jpeg";
    public const string Png = "image/png";
    public const string Heic = "image/heic";

    private static readonly byte[] JpegMagic = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] ZipMagic = [0x50, 0x4B, 0x03, 0x04];

    public static bool IsPhoto(string contentType) => contentType is Jpeg or Png or Heic;

    /// <param name="head">At least the first 12 bytes.</param>
    /// <param name="body">The whole file, to tell a DOCX from other zip files.</param>
    public static string? Detect(ReadOnlySpan<byte> head, ReadOnlySpan<byte> body)
    {
        if (head.StartsWith("%PDF-"u8))
        {
            return Pdf;
        }

        if (head.StartsWith(JpegMagic))
        {
            return Jpeg;
        }

        if (head.StartsWith(PngMagic))
        {
            return Png;
        }

        if (head.Length >= 12 && head[4..8].SequenceEqual("ftyp"u8) &&
            (head[8..12].SequenceEqual("heic"u8) || head[8..12].SequenceEqual("heix"u8) || head[8..12].SequenceEqual("mif1"u8)))
        {
            return Heic;
        }

        // A DOCX is a zip whose entries include word/document.xml (entry names are stored uncompressed).
        if (head.StartsWith(ZipMagic) && body.IndexOf("word/document.xml"u8) >= 0)
        {
            return Docx;
        }

        return null;
    }

    /// <summary>S3: a password-protected PDF names an /Encrypt dictionary in its trailer.</summary>
    public static bool IsEncryptedPdf(ReadOnlySpan<byte> pdf) => pdf.IndexOf("/Encrypt"u8) >= 0;

    /// <summary>Only the last path segment, without control characters, at most 200 characters.</summary>
    public static string SafeName(string name)
    {
        var last = Path.GetFileName(name.Replace('\\', '/'));
        var clean = new StringBuilder(last.Length);
        foreach (var c in last.Where(c => !char.IsControl(c)))
        {
            clean.Append(c);
        }

        var result = clean.ToString();
        return result.Length switch
        {
            0 => "cv",
            > 200 => result[..200],
            _ => result,
        };
    }
}
