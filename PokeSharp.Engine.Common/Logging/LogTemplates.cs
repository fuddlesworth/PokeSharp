using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace PokeSharp.Engine.Common.Logging;

/// <summary>
///     Reusable logging templates with rich Spectre.Console formatting.
///     Provides consistent visual patterns for common logging scenarios.
/// </summary>
public static class LogTemplates
{
    private enum LogAccent
    {
        Initialization,
        Asset,
        Map,
        Performance,
        Memory,
        Render,
        Entity,
        Input,
        Workflow,
        System,
    }

    private static readonly Dictionary<LogAccent, (string Glyph, string Color)> AccentStyles = new()
    {
        { LogAccent.Initialization, ("▶", "skyblue1") },
        { LogAccent.Asset, ("A", "aqua") },
        { LogAccent.Map, ("M", "springgreen1") },
        { LogAccent.Performance, ("P", "plum1") },
        { LogAccent.Memory, ("MEM", "lightsteelblue1") },
        { LogAccent.Render, ("R", "mediumorchid1") },
        { LogAccent.Entity, ("E", "gold1") },
        { LogAccent.Input, ("I", "deepskyblue3") },
        { LogAccent.Workflow, ("WF", "steelblue1") },
        { LogAccent.System, ("SYS", "orange3") },
    };

    // ═══════════════════════════════════════════════════════════════
    // Initialization Templates
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    ///     Logs system initialization with success indicator.
    /// </summary>
    public static void LogSystemInitialized(
        this ILogger logger,
        string systemName,
        params (string key, object value)[] details
    )
    {
        var detailsFormatted = FormatDetails(details);
        var body = $"[cyan]{EscapeMarkup(systemName)}[/] [dim]initialized[/]{detailsFormatted}";
        logger.LogInformation(
            LogFormatting.FormatTemplate(WithAccent(LogAccent.Initialization, body))
        );
    }

    /// <summary>
    ///     Logs component initialization with count.
    /// </summary>
    public static void LogComponentInitialized(this ILogger logger, string componentName, int count)
    {
        var body =
            $"[cyan]{EscapeMarkup(componentName)}[/] [dim]ready[/] [grey]|[/] [yellow]{count}[/] [dim]items[/]";
        logger.LogInformation(
            LogFormatting.FormatTemplate(WithAccent(LogAccent.Initialization, body))
        );
    }

    /// <summary>
    ///     Logs resource loaded successfully.
    /// </summary>
    public static void LogResourceLoaded(
        this ILogger logger,
        string resourceType,
        string resourceId,
        params (string key, object value)[] details
    )
    {
        var detailsFormatted = FormatDetails(details);
        var body =
            $"[green]Loaded[/] [cyan]{EscapeMarkup(resourceType)}[/] '[yellow]{EscapeMarkup(resourceId)}[/]'{detailsFormatted}";
        logger.LogInformation(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
    }

    // ═══════════════════════════════════════════════════════════════
    // Entity Templates
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    ///     Logs entity spawned from template.
    /// </summary>
    public static void LogEntitySpawned(
        this ILogger logger,
        string entityType,
        int entityId,
        string templateId,
        int x,
        int y
    )
    {
        var body =
            $"[yellow]{EscapeMarkup(entityType)}[/] [dim]#{entityId}[/] [dim]from[/] '[cyan]{EscapeMarkup(templateId)}[/]' [dim]at[/] [magenta]({x}, {y})[/]";
        logger.LogInformation(LogFormatting.FormatTemplate(WithAccent(LogAccent.Entity, body)));
    }

    /// <summary>
    ///     Logs entity created.
    /// </summary>
    public static void LogEntityCreated(
        this ILogger logger,
        string entityType,
        int entityId,
        params (string key, object value)[] components
    )
    {
        var componentList = FormatComponents(components);
        var body = $"[yellow]{EscapeMarkup(entityType)}[/] [dim]#{entityId}[/]{componentList}";
        logger.LogInformation(LogFormatting.FormatTemplate(WithAccent(LogAccent.Entity, body)));
    }

    // ═══════════════════════════════════════════════════════════════
    // Asset Loading Templates
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    ///     Logs asset loading started.
    /// </summary>
    public static void LogAssetLoadingStarted(this ILogger logger, string assetType, int count)
    {
        var body = $"[dim]loading[/] [yellow]{count}[/] [grey]{EscapeMarkup(assetType)}[/]";
        logger.LogInformation(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
    }

    /// <summary>
    ///     Logs asset loaded with timing.
    /// </summary>
    public static void LogAssetLoadedWithTiming(
        this ILogger logger,
        string assetId,
        double timeMs,
        int width,
        int height
    )
    {
        var timeColor = timeMs > 100 ? "yellow" : "green";
        var body =
            $"[cyan]{EscapeMarkup(assetId)}[/] [{timeColor}]{timeMs:F1}ms[/] [dim]({width}x{height}px)[/]";
        logger.LogDebug(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
    }

    /// <summary>
    ///     Logs a generic asset status line with optional metrics.
    /// </summary>
    public static void LogAssetStatus(
        this ILogger logger,
        string message,
        params (string key, object value)[] details
    )
    {
        var detailsFormatted = FormatDetails(details);
        var body = $"[cyan]{EscapeMarkup(message)}[/]{detailsFormatted}";
        logger.LogInformation(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
    }

    /// <summary>
    ///     Logs a workflow/process status message with optional details.
    /// </summary>
    public static void LogWorkflowStatus(
        this ILogger logger,
        string message,
        params (string key, object value)[] details
    )
    {
        var detailsFormatted = FormatDetails(details);
        var body = $"[cyan]{EscapeMarkup(message)}[/]{detailsFormatted}";
        logger.LogInformation(LogFormatting.FormatTemplate(WithAccent(LogAccent.Workflow, body)));
    }

    /// <summary>
    ///     Logs map loaded with statistics.
    /// </summary>
    public static void LogMapLoaded(
        this ILogger logger,
        string mapName,
        int width,
        int height,
        int tiles,
        int objects
    )
    {
        var body =
            $"[cyan]{EscapeMarkup(mapName)}[/] [dim]{width}x{height}[/] [grey]|[/] [yellow]{tiles}[/] [dim]tiles[/] [grey]|[/] [magenta]{objects}[/] [dim]objects[/]";
        logger.LogInformation(LogFormatting.FormatTemplate(WithAccent(LogAccent.Map, body)));
    }

    // ═══════════════════════════════════════════════════════════════
    // Performance Templates
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    ///     Logs frame performance statistics.
    /// </summary>
    public static void LogFramePerformance(
        this ILogger logger,
        float avgMs,
        float fps,
        float minMs,
        float maxMs
    )
    {
        var fpsColor =
            fps >= 60 ? "green"
            : fps >= 30 ? "yellow"
            : "red";
        var body =
            $"[cyan]{avgMs:F1}ms[/] [dim]avg[/] [{fpsColor}]{fps:F1} FPS[/] [dim]|[/] [aqua]{minMs:F1}ms[/] [dim]min[/] [orange1]{maxMs:F1}ms[/] [dim]peak[/]";
        logger.LogInformation(
            LogFormatting.FormatTemplate(WithAccent(LogAccent.Performance, body))
        );
    }

    /// <summary>
    ///     Logs system performance statistics.
    /// </summary>
    public static void LogSystemPerformance(
        this ILogger logger,
        string systemName,
        double avgMs,
        double maxMs,
        long calls
    )
    {
        var avgColor =
            avgMs > 1.67 ? "red"
            : avgMs > 0.84 ? "yellow"
            : "green";
        var peakColor =
            maxMs > 2.0 ? "red1"
            : maxMs > 1.0 ? "orange1"
            : "aqua";
        var systemDisplay = PadRightInvariant(EscapeMarkup(systemName), 22);
        var avgText = avgMs.ToString("0.00", CultureInfo.InvariantCulture).PadLeft(6);
        var maxText = maxMs.ToString("0.00", CultureInfo.InvariantCulture).PadLeft(6);
        var callsText = calls.ToString("N0", CultureInfo.InvariantCulture);
        var body =
            $"[cyan]{systemDisplay}[/] [{avgColor}]{avgText}ms[/] [dim]avg[/] [{peakColor}]{maxText}ms[/] [dim]peak[/] [grey]|[/] [grey]{callsText} calls[/]";
        logger.LogInformation(
            LogFormatting.FormatTemplate(WithAccent(LogAccent.Performance, body))
        );
    }

    /// <summary>
    ///     Logs memory statistics with GC info.
    /// </summary>
    public static void LogMemoryStatistics(
        this ILogger logger,
        double memoryMb,
        int gen0,
        int gen1,
        int gen2
    )
    {
        var memColor =
            memoryMb > 500 ? "red"
            : memoryMb > 250 ? "yellow"
            : "green";
        var body =
            $"[{memColor}]{memoryMb:F1}MB[/] [dim]in use[/] [grey]|[/] [grey]G0[/]: [yellow]{gen0}[/] [grey]G1[/]: [yellow]{gen1}[/] [grey]G2[/]: [yellow]{gen2}[/]";
        logger.LogInformation(LogFormatting.FormatTemplate(WithAccent(LogAccent.Memory, body)));
    }

    // ═══════════════════════════════════════════════════════════════
    // Warning Templates
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    ///     Logs slow operation warning.
    /// </summary>
    public static void LogSlowOperation(
        this ILogger logger,
        string operation,
        double timeMs,
        double thresholdMs
    )
    {
        var body =
            $"[yellow]Slow operation[/] [grey]|[/] [cyan]{EscapeMarkup(operation)}[/] [dim]took[/] [red]{timeMs:F1}ms[/] [dim](>{thresholdMs:F1}ms)[/]";
        logger.LogWarning(LogFormatting.FormatTemplate(WithAccent(LogAccent.Performance, body)));
    }

    /// <summary>
    ///     Logs slow system warning with enhanced formatting.
    /// </summary>
    public static void LogSlowSystem(
        this ILogger logger,
        string systemName,
        double timeMs,
        double percent
    )
    {
        // Color code based on severity levels:
        // >50% = Critical (red bold, double warning)
        // >20% = High (red, double warning)
        // >10% = Medium (orange/yellow, single warning)
        string icon,
            timeColor,
            percentColor,
            label;

        if (percent > 50)
        {
            icon = "[red bold on yellow]!!![/]";
            timeColor = "red bold";
            percentColor = "red bold";
            label = "[red bold]CRITICAL:[/]";
        }
        else if (percent > 20)
        {
            icon = "[red bold]!![/]";
            timeColor = "red";
            percentColor = "red bold";
            label = "[red]SLOW:[/]";
        }
        else
        {
            icon = "[yellow]![/]";
            timeColor = "yellow";
            percentColor = "orange1";
            label = "[yellow]Slow:[/]";
        }

        var message =
            $"{icon} {label} [cyan bold]{EscapeMarkup(systemName)}[/] [{timeColor}]{timeMs:F2}ms[/] "
            + $"[dim]│[/] [{percentColor}]{percent:F1}%[/] [dim]of frame[/]";
        logger.LogWarning(LogFormatting.FormatTemplate(message));
    }

    /// <summary>
    ///     Logs resource not found warning.
    /// </summary>
    public static void LogResourceNotFound(
        this ILogger logger,
        string resourceType,
        string resourceId
    )
    {
        var body =
            $"[cyan]{EscapeMarkup(resourceType)}[/] '[red]{EscapeMarkup(resourceId)}[/]' [dim]not found[/]";
        logger.LogWarning(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
    }

    /// <summary>
    ///     Logs operation skipped with reason.
    /// </summary>
    public static void LogOperationSkipped(this ILogger logger, string operation, string reason)
    {
        var body =
            $"[yellow]Skipped[/] [grey]|[/] [cyan]{EscapeMarkup(operation)}[/] [dim]({EscapeMarkup(reason)})[/]";
        logger.LogWarning(LogFormatting.FormatTemplate(WithAccent(LogAccent.Workflow, body)));
    }

    // ═══════════════════════════════════════════════════════════════
    // Error Templates
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    ///     Logs operation failed with recovery action.
    /// </summary>
    public static void LogOperationFailedWithRecovery(
        this ILogger logger,
        string operation,
        string recovery
    )
    {
        var body =
            $"[red]Failed[/] [grey]|[/] [cyan]{EscapeMarkup(operation)}[/] [dim]→[/] [yellow]{EscapeMarkup(recovery)}[/]";
        logger.LogWarning(LogFormatting.FormatTemplate(WithAccent(LogAccent.Workflow, body)));
    }

    /// <summary>
    ///     Logs critical error.
    /// </summary>
    public static void LogCriticalError(this ILogger logger, Exception ex, string operation)
    {
        var body =
            $"[red bold]CRITICAL[/] [grey]|[/] [cyan]{EscapeMarkup(operation)}[/] [dim]→[/] [red]{EscapeMarkup(ex.GetType().Name)}[/]: {EscapeMarkup(ex.Message)}";
        logger.LogError(LogFormatting.FormatTemplate(WithAccent(LogAccent.System, body)));
    }

    // ═══════════════════════════════════════════════════════════════
    // Scripting & API Templates
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    ///     Logs when a dependent system is not ready or unavailable.
    /// </summary>
    public static void LogSystemUnavailable(
        this ILogger logger,
        string systemName,
        string reason,
        bool isCritical = false
    )
    {
        var label = isCritical ? "[red bold]SYSTEM OFFLINE[/]" : "[yellow]System not ready[/]";
        var body =
            $"{label} [grey]|[/] [cyan]{EscapeMarkup(systemName)}[/] [dim]→[/] [orange1]{EscapeMarkup(reason)}[/]";
        var formatted = LogFormatting.FormatTemplate(WithAccent(LogAccent.System, body));
        if (isCritical)
            logger.LogError(formatted);
        else
            logger.LogWarning(formatted);
    }

    /// <summary>
    ///     Logs when a system is missing a required dependency.
    /// </summary>
    public static void LogSystemDependencyMissing(
        this ILogger logger,
        string systemName,
        string dependencyName,
        bool isCritical = false
    )
    {
        var severity = isCritical ? "red bold" : "yellow";
        var body =
            $"[{severity}]Dependency missing[/] [grey]|[/] [cyan]{EscapeMarkup(systemName)}[/] [dim]needs[/] [yellow]{EscapeMarkup(dependencyName)}[/]";
        var formatted = LogFormatting.FormatTemplate(WithAccent(LogAccent.System, body));
        if (isCritical)
            logger.LogError(formatted);
        else
            logger.LogWarning(formatted);
    }

    /// <summary>
    ///     Logs when an entity is missing a required component for an operation.
    /// </summary>
    public static void LogEntityMissingComponent(
        this ILogger logger,
        string entityLabel,
        string componentName,
        string context
    )
    {
        var body =
            $"[yellow]{EscapeMarkup(entityLabel)}[/] [dim]missing[/] [red]{EscapeMarkup(componentName)}[/] [dim]→[/] [orange1]{EscapeMarkup(context)}[/]";
        logger.LogWarning(LogFormatting.FormatTemplate(WithAccent(LogAccent.Entity, body)));
    }

    /// <summary>
    ///     Logs when an entity is not found for an operation.
    /// </summary>
    public static void LogEntityNotFound(this ILogger logger, string entityLabel, string context)
    {
        var body =
            $"[yellow]{EscapeMarkup(entityLabel)}[/] [dim]not found[/] [dim]→[/] [orange1]{EscapeMarkup(context)}[/]";
        logger.LogWarning(LogFormatting.FormatTemplate(WithAccent(LogAccent.Entity, body)));
    }

    /// <summary>
    ///     Logs when an entity operation is invalid or skipped.
    /// </summary>
    public static void LogEntityOperationInvalid(
        this ILogger logger,
        string entityLabel,
        string operation,
        string reason
    )
    {
        var body =
            $"[yellow]{EscapeMarkup(entityLabel)}[/] [dim]→[/] [cyan]{EscapeMarkup(operation)}[/] [orange1]skipped[/] [dim]({EscapeMarkup(reason)})[/]";
        logger.LogWarning(LogFormatting.FormatTemplate(WithAccent(LogAccent.Entity, body)));
    }

    /// <summary>
    ///     Logs when a template is missing.
    /// </summary>
    public static void LogTemplateMissing(this ILogger logger, string templateId)
    {
        var body =
            $"[red bold]Template missing[/] [grey]|[/] '[yellow]{EscapeMarkup(templateId)}[/]'";
        logger.LogError(LogFormatting.FormatTemplate(WithAccent(LogAccent.Workflow, body)));
    }

    /// <summary>
    ///     Logs when a template compiler is missing for a given type.
    /// </summary>
    public static void LogTemplateCompilerMissing(this ILogger logger, string entityTypeName)
    {
        var body =
            $"[red bold]Compiler missing[/] [grey]|[/] [yellow]{EscapeMarkup(entityTypeName)}[/]";
        logger.LogError(LogFormatting.FormatTemplate(WithAccent(LogAccent.Workflow, body)));
    }

    private static string EscapeMarkup(string text)
    {
        return LogFormatting.EscapeMarkup(text);
    }

    // ═══════════════════════════════════════════════════════════════
    // Progress Templates
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    ///     Logs batch operation started.
    /// </summary>
    public static void LogBatchStarted(this ILogger logger, string operation, int total)
    {
        var body =
            $"[cyan]{EscapeMarkup(operation)}[/] [dim]started[/] [grey]|[/] [yellow]{total}[/] [dim]items[/]";
        logger.LogInformation(LogFormatting.FormatTemplate(WithAccent(LogAccent.Workflow, body)));
    }

    /// <summary>
    ///     Logs batch operation completed.
    /// </summary>
    public static void LogBatchCompleted(
        this ILogger logger,
        string operation,
        int successful,
        int failed,
        double timeMs
    )
    {
        var successColor = failed == 0 ? "green" : "yellow";
        var timeColor = failed == 0 ? "aqua" : "yellow";
        var body =
            $"[green]Completed[/] [grey]|[/] [cyan]{EscapeMarkup(operation)}[/] [{successColor}]{successful} OK[/] [dim]{failed} failed[/] [grey]|[/] [{timeColor}]{timeMs:F1}ms[/]";
        logger.LogInformation(LogFormatting.FormatTemplate(WithAccent(LogAccent.Workflow, body)));
    }

    // ═══════════════════════════════════════════════════════════════
    // Input/Interaction Templates
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    ///     Logs input controls hint.
    /// </summary>
    public static void LogControlsHint(this ILogger logger, string hint)
    {
        var body = $"[grey]Controls[/] [grey]|[/] [dim]{EscapeMarkup(hint)}[/]";
        logger.LogInformation(LogFormatting.FormatTemplate(WithAccent(LogAccent.Input, body)));
    }

    /// <summary>
    ///     Logs zoom change.
    /// </summary>
    public static void LogZoomChanged(this ILogger logger, string preset, float zoom)
    {
        var body = $"[cyan]{EscapeMarkup(preset)}[/] [dim]zoom[/] [yellow]{zoom:F1}x[/]";
        logger.LogDebug(LogFormatting.FormatTemplate(WithAccent(LogAccent.Input, body)));
    }

    /// <summary>
    ///     Logs render statistics.
    /// </summary>
    public static void LogRenderStats(
        this ILogger logger,
        int totalEntities,
        int tiles,
        int sprites,
        ulong calls
    )
    {
        var callsText = calls.ToString("N0", CultureInfo.InvariantCulture);
        var body =
            $"[cyan bold]{totalEntities}[/] [dim]entities[/] [grey]|[/] [yellow]{tiles}[/] [dim]tiles[/] [grey]|[/] [magenta]{sprites}[/] [dim]sprites[/] [grey]|[/] [grey]{callsText} calls[/]";
        logger.LogInformation(LogFormatting.FormatTemplate(WithAccent(LogAccent.Render, body)));
    }

    // ═══════════════════════════════════════════════════════════════
    // Diagnostic Templates
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    ///     Logs diagnostic header.
    /// </summary>
    public static void LogDiagnosticHeader(this ILogger logger, string title)
    {
        logger.LogInformation(
            LogFormatting.FormatTemplate(
                "[blue bold]╔══════════════════════════════════════════╗[/]"
            )
        );
        var headerLine = LogFormatting.FormatTemplate(
            $"[blue bold]║[/]  [cyan bold]{title, -38}[/]  [blue bold]║[/]"
        );
        logger.LogInformation(headerLine);
        logger.LogInformation(
            LogFormatting.FormatTemplate(
                "[blue bold]╚══════════════════════════════════════════╝[/]"
            )
        );
    }

    /// <summary>
    ///     Logs diagnostic info line.
    /// </summary>
    public static void LogDiagnosticInfo(this ILogger logger, string label, object value)
    {
        logger.LogInformation(
            LogFormatting.FormatTemplate($"[grey]→[/] [cyan]{label}:[/] [yellow]{value}[/]")
        );
    }

    /// <summary>
    ///     Logs diagnostic separator.
    /// </summary>
    public static void LogDiagnosticSeparator(this ILogger logger)
    {
        logger.LogInformation(
            LogFormatting.FormatTemplate("[dim]═══════════════════════════════════════════[/]")
        );
    }

    // ═══════════════════════════════════════════════════════════════
    // Helper Methods
    // ═══════════════════════════════════════════════════════════════

    private static string WithAccent(LogAccent accent, string message)
    {
        var style = AccentStyles.TryGetValue(accent, out var value) ? value : ("•", "grey");
        var glyph = style.Item1;
        return $"[{style.Item2}]{glyph.PadRight(3)}[/] {message}";
    }

    private static string PadRightInvariant(string value, int width)
    {
        if (string.IsNullOrEmpty(value))
            return new string(' ', width);
        if (value.Length >= width)
            return value.Length == width ? value : value[..width];
        return value + new string(' ', width - value.Length);
    }

    private static string FormatDetails(params (string key, object value)[] details)
    {
        if (details == null || details.Length == 0)
            return "";

        var formatted = string.Join(
            ", ",
            details.Select(d =>
                $"[dim]{EscapeMarkup(d.key)}:[/] [aqua]{EscapeMarkup(d.value?.ToString() ?? "")}[/]"
            )
        );
        return $" [dim]|[/] {formatted}";
    }

    private static string FormatComponents(params (string key, object value)[] components)
    {
        if (components == null || components.Length == 0)
            return "";

        var formatted = string.Join(
            "[dim],[/] ",
            components.Select(c => $"[aqua]{EscapeMarkup(c.key)}[/]")
        );
        return $" [dim][[{formatted}]][/]";
    }
}
