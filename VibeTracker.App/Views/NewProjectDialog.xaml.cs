using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using VibeTracker.App.Services;
using VibeTracker.Core;

namespace VibeTracker.App.Views;

public partial class NewProjectDialog : Window
{
    private readonly string _installPath;
    private readonly ProjectIndexService _indexService;

    public NewProjectDialog(string installPath, ProjectIndexService indexService)
    {
        InitializeComponent();
        _installPath = installPath;
        _indexService = indexService;

        // 从设置加载标签
        var tags = SettingsWindow.GetTags();
        foreach (var t in tags)
            CmbTag.Items.Add(new ComboBoxItem { Content = t });
        CmbTag.SelectedIndex = tags.IndexOf("轻量应用") >= 0 ? tags.IndexOf("轻量应用") : 0;
    }

    // 简化：不使用 FolderBrowserDialog 避免引入 WinForms 依赖
    // 用户直接键入或粘贴路径
    private void BrowsePath_Click(object sender, RoutedEventArgs e)
    {
        // 使用 WPF 原生方式：让用户手动输入路径
        // 如果剪贴板有内容，自动粘贴
        var clipboard = Clipboard.GetText();
        if (!string.IsNullOrWhiteSpace(clipboard) && Directory.Exists(clipboard))
        {
            TxtPath.Text = clipboard;
            if (string.IsNullOrWhiteSpace(TxtName.Text))
                TxtName.Text = new DirectoryInfo(clipboard).Name;
        }
        else
        {
            MessageBox.Show("请在路径框中直接输入或粘贴项目文件夹路径。\n\n提示：在文件资源管理器中复制文件夹路径后点击此按钮可自动填入。",
                "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Create_Click(object sender, RoutedEventArgs e)
    {
        var name = TxtName.Text.Trim();
        var path = TxtPath.Text.Trim();
        var seed = TxtSeed.Text.Trim();
        var tag = ((ComboBoxItem)CmbTag.SelectedItem)?.Content?.ToString() ?? "轻量应用";

        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("请输入项目名称。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(path))
        {
            MessageBox.Show("请选择项目文件夹。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!Directory.Exists(path))
        {
            MessageBox.Show("选择的文件夹不存在。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var agents = new List<string>();
            if (ChkClaude.IsChecked == true) agents.Add("claude");
            if (ChkCodex.IsChecked == true) agents.Add("codex");
            if (ChkClaudeCode.IsChecked == true) agents.Add("claude-code");

            // 1. 初始化 .vibe/
            var fileEngine = new FileEngine(path);
            var generator = new TemplateGenerator(fileEngine);
            generator.Initialize(name, seed, new List<string> { tag });

            // 2. 注册到索引
            _indexService.Add(name, path, tag, agents);

            // 3. 写入 Agent 配置
            var configService = new AgentConfigService(_installPath, path, _indexService.Find(path)!.Id);
            var messages = new List<string>();
            var hasConfigFailure = false;

            if (ChkClaude.IsChecked == true)
            {
                var (ok, msg) = configService.ConfigureClaudeDesktop();
                hasConfigFailure |= !ok;
                messages.Add($"Claude Desktop: {msg}");
            }
            if (ChkCodex.IsChecked == true)
            {
                var (ok, msg) = configService.ConfigureCodexDesktop();
                hasConfigFailure |= !ok;
                messages.Add($"Codex Desktop: {msg}");
            }
            if (ChkClaudeCode.IsChecked == true)
            {
                var (ok, msg) = configService.ConfigureClaudeCode();
                hasConfigFailure |= !ok;
                messages.Add($"Claude Code: {msg}");
            }

            var summary = string.Join("\n", messages);
            var title = hasConfigFailure ? "项目已创建，Agent 配置需处理" : "创建成功";
            var body = hasConfigFailure
                ? $"项目 \"{name}\" 已创建，但部分 Agent 未成功启用。\n\n{summary}\n\n可在主界面选择项目后按 F2 重新配置。"
                : $"项目 \"{name}\" 创建成功！\n\n{summary}\n\n请重启对应的 Desktop 端以使 MCP 配置生效。";
            MessageBox.Show(
                body,
                title,
                MessageBoxButton.OK,
                hasConfigFailure ? MessageBoxImage.Warning : MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"创建失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
