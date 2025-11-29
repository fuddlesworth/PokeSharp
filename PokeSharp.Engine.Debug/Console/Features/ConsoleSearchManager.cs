using PokeSharp.Engine.Debug.Console.UI;

namespace PokeSharp.Engine.Debug.Console.Features;

/// <summary>
///     Manages console search functionality (forward search and reverse-i-search).
///     Extracted from QuakeConsole to follow Single Responsibility Principle.
/// </summary>
public class ConsoleSearchManager
{
    // Section folding state for search
    // Tracks sections that were auto-expanded for search results
    private readonly HashSet<string> _autoExpandedSections = new();

    // Reverse-i-search state

    // Forward search state
    private int? _lastSearchMatchLineIndex;

    /// <summary>
    ///     Gets the output searcher instance.
    /// </summary>
    public OutputSearcher OutputSearcher { get; } = new();

    /// <summary>
    ///     Gets whether forward search mode is active.
    /// </summary>
    public bool IsSearchMode { get; private set; }

    /// <summary>
    ///     Gets the current forward search input.
    /// </summary>
    public string SearchInput { get; private set; } = string.Empty;

    /// <summary>
    ///     Gets whether reverse-i-search mode is active.
    /// </summary>
    public bool IsReverseSearchMode { get; private set; }

    /// <summary>
    ///     Gets the current reverse-i-search input.
    /// </summary>
    public string ReverseSearchInput { get; private set; } = string.Empty;

    /// <summary>
    ///     Gets the reverse search matches list.
    /// </summary>
    public List<string> ReverseSearchMatches { get; private set; } = new();

    /// <summary>
    ///     Gets the current reverse search match index.
    /// </summary>
    public int ReverseSearchIndex { get; private set; }

    /// <summary>
    ///     Gets the current reverse-i-search match.
    /// </summary>
    public string? CurrentReverseSearchMatch =>
        ReverseSearchMatches.Count > 0 && ReverseSearchIndex < ReverseSearchMatches.Count
            ? ReverseSearchMatches[ReverseSearchIndex]
            : null;

    #region Forward Search

    /// <summary>
    ///     Starts forward search mode.
    /// </summary>
    public void StartSearch()
    {
        IsSearchMode = true;
        SearchInput = string.Empty;
    }

    /// <summary>
    ///     Exits forward search mode.
    /// </summary>
    public void ExitSearch(ConsoleOutput output)
    {
        IsSearchMode = false;
        SearchInput = string.Empty;
        OutputSearcher.ClearSearch();

        // Restore folding state for auto-expanded sections
        RestoreAutoExpandedSections(output);
    }

    /// <summary>
    ///     Updates the forward search query.
    /// </summary>
    public void UpdateSearchQuery(string query, ConsoleOutput output)
    {
        SearchInput = query;
        var outputLines = output.GetAllLines().Select(line => line.Text).ToList();
        OutputSearcher.StartSearch(query, outputLines);

        // Scroll to the current match if found
        if (OutputSearcher.IsSearching)
        {
            var match = OutputSearcher.GetCurrentMatch();
            if (match != null)
                NavigateToMatch(match.LineIndex, output);
        }
    }

    /// <summary>
    ///     Navigates to the next search match.
    /// </summary>
    public void NextSearchMatch(ConsoleOutput output)
    {
        if (!OutputSearcher.IsSearching)
            return;

        OutputSearcher.NextMatch();
        var match = OutputSearcher.GetCurrentMatch();
        if (match != null)
            NavigateToMatch(match.LineIndex, output);
    }

    /// <summary>
    ///     Navigates to the previous search match.
    /// </summary>
    public void PreviousSearchMatch(ConsoleOutput output)
    {
        if (!OutputSearcher.IsSearching)
            return;

        OutputSearcher.PreviousMatch();
        var match = OutputSearcher.GetCurrentMatch();
        if (match != null)
            NavigateToMatch(match.LineIndex, output);
    }

    #endregion

    #region Reverse-i-search

    /// <summary>
    ///     Starts reverse-i-search mode.
    /// </summary>
    public void StartReverseSearch()
    {
        IsReverseSearchMode = true;
        ReverseSearchInput = string.Empty;
        ReverseSearchMatches.Clear();
        ReverseSearchIndex = 0;
    }

    /// <summary>
    ///     Exits reverse-i-search mode.
    /// </summary>
    public void ExitReverseSearch()
    {
        IsReverseSearchMode = false;
        ReverseSearchInput = string.Empty;
        ReverseSearchMatches.Clear();
        ReverseSearchIndex = 0;
    }

    /// <summary>
    ///     Updates the reverse-i-search query and finds matches.
    /// </summary>
    public void UpdateReverseSearchQuery(string query, IEnumerable<string> historyCommands)
    {
        ReverseSearchInput = query;

        if (string.IsNullOrWhiteSpace(query))
        {
            ReverseSearchMatches.Clear();
            ReverseSearchIndex = 0;
            return;
        }

        // Find all matching commands (most recent first)
        ReverseSearchMatches = historyCommands
            .Where(cmd => cmd.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Reverse()
            .ToList();

        ReverseSearchIndex = 0;
    }

    /// <summary>
    ///     Moves to the next match in reverse-i-search.
    /// </summary>
    public void ReverseSearchNextMatch()
    {
        if (ReverseSearchMatches.Count == 0)
            return;

        ReverseSearchIndex = (ReverseSearchIndex + 1) % ReverseSearchMatches.Count;
    }

    /// <summary>
    ///     Moves to the previous match in reverse-i-search.
    /// </summary>
    public void ReverseSearchPreviousMatch()
    {
        if (ReverseSearchMatches.Count == 0)
            return;

        ReverseSearchIndex =
            ReverseSearchIndex > 0 ? ReverseSearchIndex - 1 : ReverseSearchMatches.Count - 1;
    }

    /// <summary>
    ///     Gets the current reverse-i-search match for accepting.
    /// </summary>
    public string? GetCurrentMatch()
    {
        return CurrentReverseSearchMatch;
    }

    #endregion

    #region Section Folding for Search

    /// <summary>
    ///     Navigates to a search match, handling section expansion/collapse automatically.
    /// </summary>
    private void NavigateToMatch(int absoluteLineIndex, ConsoleOutput output)
    {
        // If navigating to a different match, collapse the previous auto-expanded section
        if (_lastSearchMatchLineIndex.HasValue && _lastSearchMatchLineIndex != absoluteLineIndex)
        {
            var prevSection = output.GetSectionContainingLine(_lastSearchMatchLineIndex.Value);
            if (prevSection != null && _autoExpandedSections.Contains(prevSection.Id))
            {
                output.CollapseSectionById(prevSection.Id);
                _autoExpandedSections.Remove(prevSection.Id);
            }
        }

        // Expand the section containing the new match if it's collapsed
        if (output.IsLineInCollapsedSection(absoluteLineIndex))
        {
            var expandedSection = output.ExpandSectionContainingLine(absoluteLineIndex);
            if (expandedSection != null)
                _autoExpandedSections.Add(expandedSection.Id);
        }

        // Update the last match line index (absolute)
        _lastSearchMatchLineIndex = absoluteLineIndex;

        // Convert absolute line index to effective line index (accounting for collapsed sections)
        var effectiveLineIndex = output.ConvertAbsoluteToEffectiveIndex(absoluteLineIndex);
        if (effectiveLineIndex >= 0)
            // Scroll to the effective line position
            output.ScrollToLine(effectiveLineIndex);
    }

    /// <summary>
    ///     Restores the folding state for all auto-expanded sections.
    /// </summary>
    private void RestoreAutoExpandedSections(ConsoleOutput output)
    {
        foreach (var sectionId in _autoExpandedSections)
            output.CollapseSectionById(sectionId);
        _autoExpandedSections.Clear();
        _lastSearchMatchLineIndex = null;
    }

    #endregion
}
