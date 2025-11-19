using System.Text.RegularExpressions;

namespace PokeSharp.Engine.Debug.Console.Features;

/// <summary>
/// Handles searching through console output with navigation and highlighting.
/// </summary>
public class OutputSearcher
{
    private string _searchQuery = string.Empty;
    private bool _caseSensitive = false;
    private bool _useRegex = false;
    private List<SearchMatch> _matches = new();
    private int _currentMatchIndex = -1;

    /// <summary>
    /// Represents a match in the console output.
    /// </summary>
    public record SearchMatch(int LineIndex, int StartColumn, int Length);

    /// <summary>
    /// Gets whether a search is currently active.
    /// </summary>
    public bool IsSearching => !string.IsNullOrEmpty(_searchQuery) && _matches.Count > 0;

    /// <summary>
    /// Gets the current search query.
    /// </summary>
    public string SearchQuery => _searchQuery;

    /// <summary>
    /// Gets all matches.
    /// </summary>
    public IReadOnlyList<SearchMatch> Matches => _matches;

    /// <summary>
    /// Gets the index of the currently selected match.
    /// </summary>
    public int CurrentMatchIndex => _currentMatchIndex;

    /// <summary>
    /// Gets the total number of matches.
    /// </summary>
    public int MatchCount => _matches.Count;

    /// <summary>
    /// Gets whether case-sensitive search is enabled.
    /// </summary>
    public bool IsCaseSensitive => _caseSensitive;

    /// <summary>
    /// Gets whether regex search is enabled.
    /// </summary>
    public bool IsUsingRegex => _useRegex;

    /// <summary>
    /// Starts a new search with the given query.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="outputLines">The console output lines to search through.</param>
    /// <param name="caseSensitive">Whether the search should be case-sensitive.</param>
    /// <param name="useRegex">Whether to treat the query as a regular expression.</param>
    public void StartSearch(string query, IReadOnlyList<string> outputLines, bool caseSensitive = false, bool useRegex = false)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            ClearSearch();
            return;
        }

        _searchQuery = query;
        _caseSensitive = caseSensitive;
        _useRegex = useRegex;
        _matches.Clear();
        _currentMatchIndex = -1;

        // Perform the search
        SearchInOutput(outputLines);

        // Select first match if any
        if (_matches.Count > 0)
            _currentMatchIndex = 0;
    }

    /// <summary>
    /// Moves to the next match.
    /// </summary>
    /// <returns>True if moved to next match, false if no more matches.</returns>
    public bool NextMatch()
    {
        if (_matches.Count == 0)
            return false;

        _currentMatchIndex = (_currentMatchIndex + 1) % _matches.Count;
        return true;
    }

    /// <summary>
    /// Moves to the previous match.
    /// </summary>
    /// <returns>True if moved to previous match, false if no more matches.</returns>
    public bool PreviousMatch()
    {
        if (_matches.Count == 0)
            return false;

        _currentMatchIndex = _currentMatchIndex <= 0 ? _matches.Count - 1 : _currentMatchIndex - 1;
        return true;
    }

    /// <summary>
    /// Gets the current match.
    /// </summary>
    public SearchMatch? GetCurrentMatch()
    {
        if (_currentMatchIndex < 0 || _currentMatchIndex >= _matches.Count)
            return null;

        return _matches[_currentMatchIndex];
    }

    /// <summary>
    /// Clears the current search.
    /// </summary>
    public void ClearSearch()
    {
        _searchQuery = string.Empty;
        _matches.Clear();
        _currentMatchIndex = -1;
        _caseSensitive = false;
        _useRegex = false;
    }

    /// <summary>
    /// Performs the actual search through output lines.
    /// </summary>
    private void SearchInOutput(IReadOnlyList<string> outputLines)
    {
        for (int lineIndex = 0; lineIndex < outputLines.Count; lineIndex++)
        {
            var line = outputLines[lineIndex];

            if (_useRegex)
            {
                SearchWithRegex(line, lineIndex);
            }
            else
            {
                SearchWithPlainText(line, lineIndex);
            }
        }
    }

    /// <summary>
    /// Searches a line using plain text matching.
    /// </summary>
    private void SearchWithPlainText(string line, int lineIndex)
    {
        var comparison = _caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        int startIndex = 0;

        while (startIndex < line.Length)
        {
            int index = line.IndexOf(_searchQuery, startIndex, comparison);
            if (index == -1)
                break;

            _matches.Add(new SearchMatch(lineIndex, index, _searchQuery.Length));
            startIndex = index + 1; // Move past this match to find overlapping matches
        }
    }

    /// <summary>
    /// Searches a line using regular expression matching.
    /// </summary>
    private void SearchWithRegex(string line, int lineIndex)
    {
        try
        {
            var options = _caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
            var regex = new Regex(_searchQuery, options);
            var matches = regex.Matches(line);

            foreach (Match match in matches)
            {
                _matches.Add(new SearchMatch(lineIndex, match.Index, match.Length));
            }
        }
        catch (ArgumentException)
        {
            // Invalid regex pattern - fall back to plain text search
            SearchWithPlainText(line, lineIndex);
        }
    }

    /// <summary>
    /// Updates the search with new output lines (e.g., when output changes).
    /// </summary>
    public void UpdateSearch(IReadOnlyList<string> outputLines)
    {
        if (string.IsNullOrEmpty(_searchQuery))
            return;

        int previousMatchCount = _matches.Count;
        var previousMatchLineIndex = GetCurrentMatch()?.LineIndex ?? -1;

        // Re-run the search
        _matches.Clear();
        SearchInOutput(outputLines);

        // Try to maintain the current match position
        if (_currentMatchIndex >= 0 && previousMatchLineIndex >= 0)
        {
            // Find a match on the same line or close to it
            for (int i = 0; i < _matches.Count; i++)
            {
                if (_matches[i].LineIndex >= previousMatchLineIndex)
                {
                    _currentMatchIndex = i;
                    return;
                }
            }
        }

        // If we couldn't maintain position, select first match
        _currentMatchIndex = _matches.Count > 0 ? 0 : -1;
    }
}

