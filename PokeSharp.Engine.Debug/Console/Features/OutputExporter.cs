using System.Text;
using PokeSharp.Engine.Debug.Console.UI;

namespace PokeSharp.Engine.Debug.Console.Features;

/// <summary>
/// Handles exporting console output to files.
/// </summary>
public class OutputExporter
{
    private readonly string _exportsDirectory;

    /// <summary>
    /// Result of an export operation.
    /// </summary>
    public record ExportResult(bool Success, string FilePath, int LinesExported, string? ErrorMessage = null);

    public OutputExporter(string baseDirectory)
    {
        _exportsDirectory = Path.Combine(baseDirectory, "exports");

        // Ensure exports directory exists
        if (!Directory.Exists(_exportsDirectory))
        {
            Directory.CreateDirectory(_exportsDirectory);
        }
    }

    /// <summary>
    /// Exports console output to a file.
    /// </summary>
    /// <param name="output">The console output to export.</param>
    /// <param name="filename">Optional filename. If null, auto-generates with timestamp.</param>
    /// <param name="exportAll">If true, exports entire buffer. If false, only visible lines.</param>
    /// <param name="includeMetadata">If true, includes header with metadata.</param>
    /// <returns>Result of the export operation.</returns>
    public ExportResult ExportOutput(
        ConsoleOutput output,
        string? filename = null,
        bool exportAll = false,
        bool includeMetadata = true)
    {
        try
        {
            // Generate filename if not provided
            if (string.IsNullOrWhiteSpace(filename))
            {
                filename = $"console-output-{DateTime.Now:yyyy-MM-dd-HHmmss}.txt";
            }

            // Ensure .txt extension
            if (!filename.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            {
                filename += ".txt";
            }

            // Get full path
            var fullPath = Path.Combine(_exportsDirectory, filename);

            // Get lines to export
            var lines = exportAll ? output.GetAllLines() : output.GetVisibleLines();

            if (lines.Count == 0)
            {
                return new ExportResult(false, fullPath, 0, "No output to export");
            }

            // Build content
            var sb = new StringBuilder();

            // Add metadata header if requested
            if (includeMetadata)
            {
                sb.AppendLine("╔═══════════════════════════════════════════════════════════════╗");
                sb.AppendLine("║              PokeSharp Console Output Export                 ║");
                sb.AppendLine("╚═══════════════════════════════════════════════════════════════╝");
                sb.AppendLine();
                sb.AppendLine($"Exported: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"Export Type: {(exportAll ? "Full Buffer" : "Visible Output Only")}");
                sb.AppendLine($"Total Lines: {lines.Count}");
                if (!exportAll)
                {
                    sb.AppendLine($"Total Buffer Size: {output.TotalLines} lines");
                }
                sb.AppendLine();
                sb.AppendLine("───────────────────────────────────────────────────────────────");
                sb.AppendLine();
            }

            // Add console output (strip color information, just text)
            foreach (var line in lines)
            {
                sb.AppendLine(line.Text);
            }

            // Add footer if metadata enabled
            if (includeMetadata)
            {
                sb.AppendLine();
                sb.AppendLine("───────────────────────────────────────────────────────────────");
                sb.AppendLine($"End of export - {lines.Count} lines");
            }

            // Write to file
            File.WriteAllText(fullPath, sb.ToString());

            return new ExportResult(true, fullPath, lines.Count);
        }
        catch (Exception ex)
        {
            return new ExportResult(false, filename ?? "unknown", 0, ex.Message);
        }
    }

    /// <summary>
    /// Gets the exports directory path.
    /// </summary>
    public string GetExportsDirectory() => _exportsDirectory;

    /// <summary>
    /// Lists all exported files.
    /// </summary>
    public List<string> ListExports()
    {
        try
        {
            if (!Directory.Exists(_exportsDirectory))
                return new List<string>();

            return Directory.GetFiles(_exportsDirectory, "*.txt")
                .Select(Path.GetFileName)
                .Where(f => f != null)
                .Cast<string>()
                .OrderByDescending(f => f)
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    /// <summary>
    /// Deletes all export files.
    /// </summary>
    public int ClearExports()
    {
        try
        {
            if (!Directory.Exists(_exportsDirectory))
                return 0;

            var files = Directory.GetFiles(_exportsDirectory, "*.txt");
            foreach (var file in files)
            {
                File.Delete(file);
            }
            return files.Length;
        }
        catch
        {
            return 0;
        }
    }
}

