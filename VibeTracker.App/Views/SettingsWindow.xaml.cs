using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace VibeTracker.App.Views;

public partial class SettingsWindow : Window
{
    private readonly string _tagsPath;
    private List<string> _tags = new();

    public SettingsWindow()
    {
        InitializeComponent();

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "VibeTracker");
        Directory.CreateDirectory(dir);
        _tagsPath = Path.Combine(dir, "tags.json");

        LoadTags();
        RenderTags();
    }

    private void LoadTags()
    {
        try
        {
            if (File.Exists(_tagsPath))
                _tags = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(_tagsPath)) ?? DefaultTags();
            else
                _tags = DefaultTags();
        }
        catch
        {
            _tags = DefaultTags();
        }

        SaveTags();
    }

    private static List<string> DefaultTags()
        => new() { "插件", "轻量应用", "大型应用", "技能", "文档" };

    private void SaveTags()
    {
        var json = JsonSerializer.Serialize(_tags, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_tagsPath, json);
    }

    private void RenderTags()
    {
        TagsPanel.Children.Clear();
        foreach (var tag in _tags)
        {
            TagsPanel.Children.Add(CreateTagChip(tag));
        }
    }

    private Border CreateTagChip(string tag)
    {
        var chip = new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xF0, 0xFE)),
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(3),
            Cursor = Cursors.Hand,
            Tag = tag
        };

        var stack = new StackPanel { Orientation = Orientation.Horizontal };

        var label = new TextBlock
        {
            Text = tag,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(0x1A, 0x56, 0xDB)),
            VerticalAlignment = VerticalAlignment.Center
        };
        stack.Children.Add(label);

        var delBtn = new TextBlock
        {
            Text = " ✕",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)),
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = Cursors.Hand,
            Tag = tag
        };
        delBtn.MouseLeftButtonDown += (s, e) =>
        {
            var t = (string)((TextBlock)s!).Tag;
            _tags.Remove(t);
            SaveTags();
            RenderTags();
            e.Handled = true;
        };
        stack.Children.Add(delBtn);

        chip.Child = stack;

        chip.MouseLeftButtonDown += (s, e) =>
        {
            if (e.ClickCount == 2)
            {
                RenameTag(tag);
            }
        };

        return chip;
    }

    private void RenameTag(string oldTag)
    {
        var input = Microsoft.VisualBasic.Interaction.InputBox(
            "编辑标签名称：", "编辑标签", oldTag);

        if (!string.IsNullOrWhiteSpace(input) && input != oldTag)
        {
            var idx = _tags.IndexOf(oldTag);
            if (idx >= 0)
            {
                _tags[idx] = input;
                SaveTags();
                RenderTags();
            }
        }
    }

    private void AddTag_Click(object sender, RoutedEventArgs e)
    {
        AddNewTag();
    }

    private void TxtNewTag_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            AddNewTag();
    }

    private void AddNewTag()
    {
        var tag = TxtNewTag.Text.Trim();
        if (string.IsNullOrWhiteSpace(tag))
            return;

        if (_tags.Contains(tag))
        {
            MessageBox.Show("标签已存在。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _tags.Add(tag);
        SaveTags();
        RenderTags();
        TxtNewTag.Text = "";
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    // 暴露给外部用的方法
    public static List<string> GetTags()
    {
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var path = Path.Combine(appData, "VibeTracker", "tags.json");
            if (File.Exists(path))
                return JsonSerializer.Deserialize<List<string>>(File.ReadAllText(path)) ?? DefaultTags();
        }
        catch { }
        return DefaultTags();
    }
}
