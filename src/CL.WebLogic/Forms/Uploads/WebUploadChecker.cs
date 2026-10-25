using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Forms.Models;
using CL.WebLogic.Forms.Services;
using CL.WebLogic.Forms.Validation;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Forms.Uploads;

/// <summary>
/// Checks uploads of file fields: count, size, extension allowlist, content sniffing (the bytes
/// must match the extension) and, for images, format and dimensions.
/// </summary>
public sealed class WebUploadChecker
{
    private const int HeaderBytes = 64 * 1024;

    private readonly WebLogicConfigHolder _config;
    private readonly WebFormMessages _messages;

    /// <summary>Creates the checker.</summary>
    public WebUploadChecker(WebLogicConfigHolder config, WebFormMessages messages)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _messages = messages ?? throw new ArgumentNullException(nameof(messages));
    }

    /// <summary>Checks and binds the files of <paramref name="field"/>.</summary>
    public async Task BindAsync(WebFieldDefinition field, string key, IFormFileCollection files, object model, WebFormErrors errors, CancellationToken cancellationToken)
    {
        var rules = field.File ?? new WebFileRules();
        var posted = files.GetFiles(key).Where(f => f.Length > 0).ToList();
        if (posted.Count > rules.MaxFiles)
        {
            errors.Add(key, _messages.Format("fileCount", field.Label, rules.MaxFiles));
            return;
        }

        var config = _config.Current.Forms;
        var maxBytes = rules.MaxBytes ?? config.MaxFileBytes;
        var extensions = rules.Extensions.Count > 0 ? rules.Extensions : config.AllowedExtensions.Select(e => e.ToLowerInvariant()).ToList();
        var accepted = new List<object?>();
        foreach (var file in posted)
        {
            var name = CleanName(file.FileName);
            var extension = Path.GetExtension(name).ToLowerInvariant();
            if (file.Length > maxBytes)
            {
                errors.Add(key, _messages.Format("fileTooLarge", field.Label, name, FormatBytes(maxBytes)));
                continue;
            }
            if (extension.Length == 0 || !extensions.Contains(extension))
            {
                errors.Add(key, _messages.Format("fileType", field.Label, name));
                continue;
            }

            var header = new byte[(int)Math.Min(HeaderBytes, file.Length)];
            await using (var stream = file.OpenReadStream())
                await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
            var detected = FileSignatureSniffer.Detect(header);
            if (!FileSignatureSniffer.Matches(extension, detected))
            {
                errors.Add(key, _messages.Format("fileContent", field.Label, name));
                continue;
            }

            int? width = null, height = null;
            if (ImageHeaderReader.TryRead(header, out var image))
            {
                width = image.Width;
                height = image.Height;
            }
            if (rules.ImagesOnly && width is null)
            {
                errors.Add(key, _messages.Format("imageOnly", field.Label, name));
                continue;
            }
            if ((rules.MaxWidth is { } maxWidth && width > maxWidth) || (rules.MaxHeight is { } maxHeight && height > maxHeight))
            {
                errors.Add(key, _messages.Format("imageSize", field.Label, name, rules.MaxWidth?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "∞",
                    rules.MaxHeight?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "∞"));
                continue;
            }
            accepted.Add(new WebUploadedFile(file, name, detected, width, height));
        }

        if (field.ElementType is not null)
            field.Property.SetValue(model, WebValueConverter.CreateCollection(field.Property.PropertyType, field.ElementType, accepted));
        else if (accepted.Count > 0)
            field.Property.SetValue(model, accepted[0]);
    }

    /// <summary>A display-safe file name: no path, no control or reserved characters, at most 255 characters.</summary>
    public static string CleanName(string? fileName)
    {
        var name = Path.GetFileName((fileName ?? "").Replace('\\', '/').Split('/')[^1]);
        name = new string([.. name.Where(c => !char.IsControl(c) && c is not ('<' or '>' or ':' or '"' or '|' or '?' or '*'))]).Trim().TrimStart('.');
        if (name.Length == 0) name = "file";
        return name.Length > 255 ? name[..255] : name;
    }

    private static string FormatBytes(long bytes) => bytes >= 1024 * 1024
        ? (bytes / (1024.0 * 1024)).ToString("0.#", System.Globalization.CultureInfo.CurrentCulture) + " MB"
        : (bytes / 1024.0).ToString("0", System.Globalization.CultureInfo.CurrentCulture) + " KB";
}
