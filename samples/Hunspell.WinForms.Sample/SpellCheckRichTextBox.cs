// Copyright (C) 2025 Hunspell.NET Contributors
// This file is part of Hunspell.NET.
// Licensed under MPL 1.1/GPL 2.0/LGPL 2.1

using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Text.RegularExpressions;

namespace Hunspell.WinForms.Sample;

/// <summary>
/// A RichTextBox with integrated spell checking that displays red squiggly underlines
/// for misspelled words. Right-click on a misspelled word to see suggestions.
/// </summary>
public partial class SpellCheckRichTextBox : RichTextBox
{
    private readonly List<(int Start, int Length)> _misspelledRanges = [];
    private readonly HashSet<string> _ignoredWords = new(StringComparer.OrdinalIgnoreCase);
    private HunspellSpellChecker? _spellChecker;
    private System.Windows.Forms.Timer? _spellCheckTimer;
    private bool _isUpdating;
    
    // Regex to match words (letters and apostrophes for contractions)
    private static readonly Regex WordPattern = new(@"\b[a-zA-Z']+\b", RegexOptions.Compiled);

    public SpellCheckRichTextBox()
    {
        // Enable custom painting
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        
        // Set up a timer to delay spell checking (debounce)
        _spellCheckTimer = new System.Windows.Forms.Timer
        {
            Interval = 500
        };
        _spellCheckTimer.Tick += SpellCheckTimer_Tick;
    }

    /// <summary>
    /// Gets or sets the spell checker to use for spell checking.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public HunspellSpellChecker? SpellChecker
    {
        get => _spellChecker;
        set
        {
            _spellChecker = value;
            CheckSpelling();
        }
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        
        // Reset the timer on each text change (debounce)
        _spellCheckTimer?.Stop();
        _spellCheckTimer?.Start();
    }

    private void SpellCheckTimer_Tick(object? sender, EventArgs e)
    {
        _spellCheckTimer?.Stop();
        CheckSpelling();
    }

    /// <summary>
    /// Performs spell checking on the entire text.
    /// </summary>
    public void CheckSpelling()
    {
        if (_spellChecker == null || _isUpdating)
            return;

        _isUpdating = true;
        
        try
        {
            _misspelledRanges.Clear();
            
            var text = Text;
            var matches = WordPattern.Matches(text);
            
            foreach (Match match in matches)
            {
                var word = match.Value;
                
                // Skip short words or words with only apostrophes
                if (word.Length < 2 || word.All(c => c == '\''))
                    continue;
                
                // Clean up the word (remove leading/trailing apostrophes)
                word = word.Trim('\'');
                
                if (string.IsNullOrEmpty(word))
                    continue;
                
                // Skip ignored words (session-specific)
                if (_ignoredWords.Contains(word))
                    continue;
                
                if (!_spellChecker.Spell(word))
                {
                    _misspelledRanges.Add((match.Index, match.Length));
                }
            }
            
            // Force redraw to show/hide underlines
            Invalidate();
        }
        finally
        {
            _isUpdating = false;
        }
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        
        // WM_PAINT
        if (m.Msg == 0x000F)
        {
            DrawSpellingErrors();
        }
    }

    /// <summary>
    /// Draws red squiggly underlines under misspelled words.
    /// This is a visual effect only and does not modify the RTF content.
    /// </summary>
    private void DrawSpellingErrors()
    {
        if (_misspelledRanges.Count == 0)
            return;

        using var graphics = CreateGraphics();
        
        foreach (var (start, length) in _misspelledRanges)
        {
            try
            {
                // Get the position of the word
                var startPoint = GetPositionFromCharIndex(start);
                var endPoint = GetPositionFromCharIndex(start + length);
                
                // If the end position is on a new line, we need to handle wrapping
                if (endPoint.X <= startPoint.X && length > 0)
                {
                    endPoint = GetPositionFromCharIndex(start + length - 1);
                    // Estimate the width of the last character
                    endPoint.X += 8;
                }
                
                // Get the baseline for the underline (approximate based on font)
                var lineHeight = (int)(Font.GetHeight(graphics));
                var baseline = startPoint.Y + lineHeight - 2;
                
                DrawSquigglyLine(graphics, startPoint.X, endPoint.X, baseline);
            }
            catch
            {
                // Ignore drawing errors for out-of-view text
            }
        }
    }

    /// <summary>
    /// Draws a red squiggly line between two x coordinates at the specified y position.
    /// </summary>
    private static void DrawSquigglyLine(Graphics graphics, int startX, int endX, int y)
    {
        if (endX <= startX)
            return;

        using var pen = new Pen(Color.Red, 1);
        
        var points = new List<Point>();
        var waveHeight = 2;
        var waveLength = 4;
        var up = true;
        
        for (int x = startX; x <= endX; x += waveLength / 2)
        {
            points.Add(new Point(x, y + (up ? -waveHeight : waveHeight)));
            up = !up;
        }
        
        if (points.Count > 1)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.DrawLines(pen, points.ToArray());
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            // Get the character index at the click position
            var charIndex = GetCharIndexFromPosition(e.Location);
            
            // Check if clicked on a misspelled word
            var misspelledRange = _misspelledRanges.FirstOrDefault(r => 
                charIndex >= r.Start && charIndex < r.Start + r.Length);
            
            if (misspelledRange != default)
            {
                // Select the misspelled word
                Select(misspelledRange.Start, misspelledRange.Length);
                
                // Show context menu with suggestions
                ShowSpellingSuggestionsMenu(misspelledRange.Start, misspelledRange.Length, e.Location);
                return;
            }
        }
        
        base.OnMouseDown(e);
    }

    /// <summary>
    /// Shows a context menu with spelling suggestions for the misspelled word.
    /// </summary>
    private void ShowSpellingSuggestionsMenu(int start, int length, Point location)
    {
        if (_spellChecker == null)
            return;

        var word = Text.Substring(start, length).Trim('\'');
        var suggestions = _spellChecker.Suggest(word);
        
        var contextMenu = new ContextMenuStrip();
        
        if (suggestions.Count > 0)
        {
            // Add suggestions (limit to 10)
            foreach (var suggestion in suggestions.Take(10))
            {
                var menuItem = new ToolStripMenuItem(suggestion);
                var suggestionText = suggestion; // Capture for closure
                menuItem.Click += (s, e) =>
                {
                    // Replace the misspelled word with the suggestion
                    Select(start, length);
                    SelectedText = suggestionText;
                    CheckSpelling();
                };
                contextMenu.Items.Add(menuItem);
            }
        }
        else
        {
            var noSuggestions = new ToolStripMenuItem("(No suggestions)")
            {
                Enabled = false
            };
            contextMenu.Items.Add(noSuggestions);
        }
        
        contextMenu.Items.Add(new ToolStripSeparator());
        
        // Add to dictionary option
        var addToDictionary = new ToolStripMenuItem($"Add \"{word}\" to dictionary");
        addToDictionary.Click += (s, e) =>
        {
            _spellChecker.Add(word);
            CheckSpelling();
        };
        contextMenu.Items.Add(addToDictionary);
        
        // Ignore option (session-specific, not saved to dictionary)
        var ignoreAll = new ToolStripMenuItem("Ignore All");
        ignoreAll.Click += (s, e) =>
        {
            _ignoredWords.Add(word);
            CheckSpelling();
        };
        contextMenu.Items.Add(ignoreAll);
        
        contextMenu.Show(this, location);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _spellCheckTimer?.Dispose();
            _spellCheckTimer = null;
        }
        base.Dispose(disposing);
    }
}
