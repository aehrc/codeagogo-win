// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using TextBox = System.Windows.Controls.TextBox;

namespace Codeagogo;

/// <summary>
/// Floating ECL reference panel displaying knowledge articles from ecl-core.
/// Articles are grouped by category and expandable with Markdown rendering.
/// </summary>
public partial class ECLReferenceWindow : Window
{
    private readonly List<ECLBridge.KnowledgeArticle> _articles;
    private readonly HashSet<string> _expandedArticles = [];

    public ECLReferenceWindow(List<ECLBridge.KnowledgeArticle> articles)
    {
        InitializeComponent();
        _articles = articles;
        RebuildTree();
    }

    private static readonly (string Key, string Display)[] Categories =
    [
        ("operator", "Operators"),
        ("refinement", "Refinements"),
        ("filter", "Filters"),
        ("pattern", "Patterns & Examples"),
        ("grammar", "Grammar"),
        ("history", "History Supplements")
    ];

    private void RebuildTree(string? filter = null)
    {
        ArticleTree.Items.Clear();

        var filtered = string.IsNullOrWhiteSpace(filter)
            ? _articles
            : _articles.Where(a =>
                a.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                a.Summary.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                a.Content.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

        TopicCount.Text = $"{filtered.Count} topics";

        foreach (var (key, display) in Categories)
        {
            var categoryArticles = filtered.Where(a => a.Category == key).ToList();
            if (categoryArticles.Count == 0) continue;

            var categoryItem = new TreeViewItem
            {
                Header = CreateCategoryHeader(display, categoryArticles.Count),
                IsExpanded = true,
                FontWeight = FontWeights.SemiBold
            };

            foreach (var article in categoryArticles)
            {
                var articleItem = new TreeViewItem
                {
                    Header = CreateArticleHeader(article),
                    Tag = article,
                    FontWeight = FontWeights.Normal
                };

                if (_expandedArticles.Contains(article.Id))
                {
                    articleItem.Items.Add(CreateArticleContent(article));
                }

                articleItem.MouseDoubleClick += (s, e) =>
                {
                    e.Handled = true;
                    ToggleArticle(articleItem, article);
                };

                articleItem.Selected += (s, e) =>
                {
                    e.Handled = true;
                    ToggleArticle(articleItem, article);
                };

                categoryItem.Items.Add(articleItem);
            }

            ArticleTree.Items.Add(categoryItem);
        }
    }

    private void ToggleArticle(TreeViewItem item, ECLBridge.KnowledgeArticle article)
    {
        if (_expandedArticles.Contains(article.Id))
        {
            _expandedArticles.Remove(article.Id);
            item.Items.Clear();
        }
        else
        {
            _expandedArticles.Add(article.Id);
            item.Items.Clear();
            item.Items.Add(CreateArticleContent(article));
            item.IsExpanded = true;
        }
    }

    private static StackPanel CreateCategoryHeader(string name, int count)
    {
        var panel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
        panel.Children.Add(new TextBlock
        {
            Text = name,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 6, 0)
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"({count})",
            FontSize = 11,
            Foreground = Brushes.Gray,
            VerticalAlignment = VerticalAlignment.Center
        });
        return panel;
    }

    private static StackPanel CreateArticleHeader(ECLBridge.KnowledgeArticle article)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 2, 0, 2) };
        panel.Children.Add(new TextBlock
        {
            Text = article.Name,
            FontSize = 12,
            FontWeight = FontWeights.Medium
        });
        panel.Children.Add(new TextBlock
        {
            Text = article.Summary,
            FontSize = 11,
            Foreground = Brushes.Gray,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 400
        });
        return panel;
    }

    private static TreeViewItem CreateArticleContent(ECLBridge.KnowledgeArticle article)
    {
        var panel = new StackPanel { Margin = new Thickness(4, 4, 4, 8) };

        // Render markdown content
        var sections = ParseMarkdownSections(article.Content);
        foreach (var section in sections)
        {
            switch (section.Kind)
            {
                case SectionKind.Code:
                    panel.Children.Add(new TextBox
                    {
                        Text = section.Text,
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 11,
                        Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                        BorderThickness = new Thickness(0),
                        Padding = new Thickness(8, 6, 8, 6),
                        IsReadOnly = true,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 2, 0, 2)
                    });
                    break;

                case SectionKind.Table:
                    panel.Children.Add(CreateTableView(section.Text));
                    break;

                case SectionKind.Prose:
                    var cleaned = CleanMarkdown(section.Text);
                    if (!string.IsNullOrWhiteSpace(cleaned))
                    {
                        panel.Children.Add(new TextBlock
                        {
                            Text = cleaned,
                            FontSize = 11,
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(0, 2, 0, 2)
                        });
                    }
                    break;
            }
        }

        // Examples
        if (article.Examples.Count > 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "Examples",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 6, 0, 2)
            });

            foreach (var example in article.Examples)
            {
                panel.Children.Add(new TextBox
                {
                    Text = example,
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 11,
                    Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(8, 4, 8, 4),
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 1, 0, 1)
                });
            }
        }

        return new TreeViewItem
        {
            Header = panel,
            IsExpanded = true,
            Focusable = false
        };
    }

    private static UIElement CreateTableView(string text)
    {
        var rows = ParseTableRows(text);
        if (rows.Count == 0) return new TextBlock();

        var grid = new Grid
        {
            Background = new SolidColorBrush(Color.FromRgb(250, 250, 250)),
            Margin = new Thickness(0, 2, 0, 2)
        };

        // Create columns
        var colCount = rows.Max(r => r.Count);
        for (int c = 0; c < colCount; c++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Create rows
        for (int r = 0; r < rows.Count; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int c = 0; c < rows[r].Count; c++)
            {
                var tb = new TextBlock
                {
                    Text = rows[r][c],
                    FontSize = 11,
                    FontWeight = r == 0 ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = r == 0 ? Brushes.Black : Brushes.Gray,
                    Padding = new Thickness(6, 3, 6, 3),
                    TextWrapping = TextWrapping.Wrap
                };
                Grid.SetRow(tb, r);
                Grid.SetColumn(tb, c);
                grid.Children.Add(tb);
            }
        }

        return grid;
    }

    // ── Markdown Helpers ────────────────────────────────────────────

    private enum SectionKind { Prose, Code, Table }

    private sealed record MarkdownSection(string Text, SectionKind Kind);

    private static List<MarkdownSection> ParseMarkdownSections(string markdown)
    {
        var sections = new List<MarkdownSection>();
        var current = "";
        var currentKind = SectionKind.Prose;
        var lines = markdown.Split('\n');

        void Flush()
        {
            var trimmed = current.Trim('\n', '\r');
            if (!string.IsNullOrEmpty(trimmed))
                sections.Add(new MarkdownSection(trimmed, currentKind));
            current = "";
        }

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            if (trimmedLine.StartsWith("```"))
            {
                if (currentKind == SectionKind.Code)
                {
                    Flush();
                    currentKind = SectionKind.Prose;
                }
                else
                {
                    Flush();
                    currentKind = SectionKind.Code;
                }
                continue;
            }

            var isTableLine = trimmedLine.StartsWith('|');
            var isSeparator = isTableLine && trimmedLine.All(c => c == '|' || c == '-' || c == ':' || c == ' ');

            if (currentKind == SectionKind.Code)
            {
                if (current.Length > 0) current += "\n";
                current += line;
            }
            else if (isTableLine)
            {
                if (currentKind != SectionKind.Table)
                {
                    Flush();
                    currentKind = SectionKind.Table;
                }
                if (!isSeparator)
                {
                    if (current.Length > 0) current += "\n";
                    current += line;
                }
            }
            else
            {
                if (currentKind == SectionKind.Table)
                {
                    Flush();
                    currentKind = SectionKind.Prose;
                }
                if (current.Length > 0) current += "\n";
                current += line;
            }
        }

        Flush();
        return sections;
    }

    private static string CleanMarkdown(string text)
    {
        return text
            .Replace("**", "")
            .Replace("`", "")
            .Replace("## ", "")
            .Replace("### ", "")
            .Replace("# ", "")
            .Trim();
    }

    private static List<List<string>> ParseTableRows(string text)
    {
        return text.Split('\n')
            .Select(line => line.Split('|')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList())
            .Where(row => row.Count > 0)
            .ToList();
    }

    // ── Event Handlers ──────────────────────────────────────────────

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RebuildTree(SearchBox.Text);
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void SpecLink_Click(object sender, RoutedEventArgs e)
    {
        var url = "https://confluence.ihtsdotools.org/display/DOCECL";
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
