// Copyright (C) 2025 Hunspell.NET Contributors
// This file is part of Hunspell.NET.
// Licensed under MPL 1.1/GPL 2.0/LGPL 2.1

namespace Hunspell.WinForms.Sample;

/// <summary>
/// Main form for the WinForms spell checking sample application.
/// </summary>
public partial class MainForm : Form
{
    private readonly HunspellSpellChecker _spellChecker;
    private readonly SpellCheckRichTextBox _richTextBox;
    private readonly MenuStrip _menuStrip;
    private readonly StatusStrip _statusStrip;
    private readonly ToolStripStatusLabel _statusLabel;
    private string? _currentFilePath;

    public MainForm()
    {
        // Initialize the spell checker with dictionary files
        var affixPath = Path.Combine(AppContext.BaseDirectory, "dictionaries", "test.aff");
        var dictionaryPath = Path.Combine(AppContext.BaseDirectory, "dictionaries", "test.dic");
        _spellChecker = new HunspellSpellChecker(affixPath, dictionaryPath);

        // Setup form properties
        Text = "Hunspell.NET WinForms Text Editor";
        Size = new Size(800, 600);
        StartPosition = FormStartPosition.CenterScreen;

        // Create menu strip
        _menuStrip = CreateMenuStrip();
        Controls.Add(_menuStrip);

        // Create status strip
        _statusStrip = new StatusStrip();
        _statusLabel = new ToolStripStatusLabel("Ready");
        _statusStrip.Items.Add(_statusLabel);
        Controls.Add(_statusStrip);

        // Create the spell-checking rich text box
        _richTextBox = new SpellCheckRichTextBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 12),
            SpellChecker = _spellChecker,
            AcceptsTab = true,
            BorderStyle = BorderStyle.None
        };
        
        // Create a panel to hold the text box with proper margins
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            BackColor = Color.White
        };
        panel.Controls.Add(_richTextBox);
        Controls.Add(panel);

        // Ensure proper z-order
        panel.BringToFront();
        _statusStrip.SendToBack();

        // Set initial sample text
        _richTextBox.Text = """
            Welcome to the Hunspell.NET WinForms Sample!

            This is a text editor with spell checking. Try typing some misspelled words like:
            - helo (should be hello)
            - wrld (should be world)
            - tset (should be test)
            - exampel (should be example)

            Right-click on a misspelled word to see suggestions from Hunspell.

            The red squiggly underlines are visual only - they are not saved to the RTF content.
            """;
    }

    private MenuStrip CreateMenuStrip()
    {
        var menuStrip = new MenuStrip();

        // File menu
        var fileMenu = new ToolStripMenuItem("&File");
        
        var newItem = new ToolStripMenuItem("&New", null, (s, e) => NewDocument())
        {
            ShortcutKeys = Keys.Control | Keys.N
        };
        
        var openItem = new ToolStripMenuItem("&Open...", null, (s, e) => OpenDocument())
        {
            ShortcutKeys = Keys.Control | Keys.O
        };
        
        var saveItem = new ToolStripMenuItem("&Save", null, (s, e) => SaveDocument())
        {
            ShortcutKeys = Keys.Control | Keys.S
        };
        
        var saveAsItem = new ToolStripMenuItem("Save &As...", null, (s, e) => SaveDocumentAs())
        {
            ShortcutKeys = Keys.Control | Keys.Shift | Keys.S
        };
        
        var exitItem = new ToolStripMenuItem("E&xit", null, (s, e) => Close())
        {
            ShortcutKeys = Keys.Alt | Keys.F4
        };

        fileMenu.DropDownItems.AddRange([newItem, openItem, new ToolStripSeparator(), 
            saveItem, saveAsItem, new ToolStripSeparator(), exitItem]);

        // Edit menu
        var editMenu = new ToolStripMenuItem("&Edit");
        
        var undoItem = new ToolStripMenuItem("&Undo", null, (s, e) => _richTextBox.Undo())
        {
            ShortcutKeys = Keys.Control | Keys.Z
        };
        
        var redoItem = new ToolStripMenuItem("&Redo", null, (s, e) => _richTextBox.Redo())
        {
            ShortcutKeys = Keys.Control | Keys.Y
        };
        
        var cutItem = new ToolStripMenuItem("Cu&t", null, (s, e) => _richTextBox.Cut())
        {
            ShortcutKeys = Keys.Control | Keys.X
        };
        
        var copyItem = new ToolStripMenuItem("&Copy", null, (s, e) => _richTextBox.Copy())
        {
            ShortcutKeys = Keys.Control | Keys.C
        };
        
        var pasteItem = new ToolStripMenuItem("&Paste", null, (s, e) => _richTextBox.Paste())
        {
            ShortcutKeys = Keys.Control | Keys.V
        };
        
        var selectAllItem = new ToolStripMenuItem("Select &All", null, (s, e) => _richTextBox.SelectAll())
        {
            ShortcutKeys = Keys.Control | Keys.A
        };

        editMenu.DropDownItems.AddRange([undoItem, redoItem, new ToolStripSeparator(),
            cutItem, copyItem, pasteItem, new ToolStripSeparator(), selectAllItem]);

        // Tools menu
        var toolsMenu = new ToolStripMenuItem("&Tools");
        
        var checkSpellingItem = new ToolStripMenuItem("&Check Spelling", null, (s, e) =>
        {
            _richTextBox.CheckSpelling();
            _statusLabel.Text = "Spell check completed";
        })
        {
            ShortcutKeys = Keys.F7
        };

        toolsMenu.DropDownItems.Add(checkSpellingItem);

        // Help menu
        var helpMenu = new ToolStripMenuItem("&Help");
        
        var aboutItem = new ToolStripMenuItem("&About", null, (s, e) =>
        {
            MessageBox.Show(
                "Hunspell.NET WinForms Sample\n\n" +
                "A text editor with integrated spell checking using Hunspell.NET.\n\n" +
                "Features:\n" +
                "• Red squiggly underlines for misspelled words\n" +
                "• Right-click for spelling suggestions\n" +
                "• Add words to dictionary\n\n" +
                "© 2025 Hunspell.NET Contributors",
                "About",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        });

        helpMenu.DropDownItems.Add(aboutItem);

        menuStrip.Items.AddRange([fileMenu, editMenu, toolsMenu, helpMenu]);
        
        return menuStrip;
    }

    private void NewDocument()
    {
        if (_richTextBox.Modified)
        {
            var result = MessageBox.Show("Do you want to save changes?", "Unsaved Changes",
                MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            
            if (result == DialogResult.Cancel)
                return;
            
            if (result == DialogResult.Yes)
                SaveDocument();
        }
        
        _richTextBox.Clear();
        _currentFilePath = null;
        Text = "Hunspell.NET WinForms Text Editor";
        _statusLabel.Text = "New document created";
    }

    private void OpenDocument()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Rich Text Format (*.rtf)|*.rtf|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
            DefaultExt = "rtf"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            try
            {
                if (dialog.FileName.EndsWith(".rtf", StringComparison.OrdinalIgnoreCase))
                {
                    _richTextBox.LoadFile(dialog.FileName, RichTextBoxStreamType.RichText);
                }
                else
                {
                    _richTextBox.Text = File.ReadAllText(dialog.FileName);
                }
                
                _currentFilePath = dialog.FileName;
                Text = $"Hunspell.NET WinForms Text Editor - {Path.GetFileName(dialog.FileName)}";
                _statusLabel.Text = $"Opened: {dialog.FileName}";
                _richTextBox.CheckSpelling();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening file: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void SaveDocument()
    {
        if (string.IsNullOrEmpty(_currentFilePath))
        {
            SaveDocumentAs();
            return;
        }

        try
        {
            if (_currentFilePath.EndsWith(".rtf", StringComparison.OrdinalIgnoreCase))
            {
                _richTextBox.SaveFile(_currentFilePath, RichTextBoxStreamType.RichText);
            }
            else
            {
                File.WriteAllText(_currentFilePath, _richTextBox.Text);
            }
            
            _statusLabel.Text = $"Saved: {_currentFilePath}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving file: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveDocumentAs()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "Rich Text Format (*.rtf)|*.rtf|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
            DefaultExt = "rtf"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _currentFilePath = dialog.FileName;
            SaveDocument();
            Text = $"Hunspell.NET WinForms Text Editor - {Path.GetFileName(dialog.FileName)}";
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _spellChecker.Dispose();
        }
        base.Dispose(disposing);
    }
}
