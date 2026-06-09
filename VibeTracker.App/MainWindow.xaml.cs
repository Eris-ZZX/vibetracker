using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VibeTracker.App.Services;
using VibeTracker.App.ViewModels;
using VibeTracker.Core.Models;

namespace VibeTracker.App;

public partial class MainWindow : Window
{
    private readonly ProjectIndexService _indexService = new();
    private DashboardViewModel? _currentDashboard;
    private string? _installPath;

    public MainWindow()
    {
        InitializeComponent();
        _installPath = AppDomain.CurrentDomain.BaseDirectory;

        // 初始状态：显示空面板
        EmptyPanel.Visibility = Visibility.Visible;
        DashboardScroll.Visibility = Visibility.Collapsed;

        RefreshProjectList();
    }

    // ═══════ 项目列表刷新 ═══════

    private void RefreshProjectList()
    {
        var (valid, invalid) = _indexService.Validate();
        var allEntries = new List<ProjectEntry>();
        allEntries.AddRange(valid);
        allEntries.AddRange(invalid);

        var currentSelectedId = (ProjectListItems.ItemsSource as List<ProjectCardViewModel>)
            ?.FirstOrDefault(c => c.IsSelected)?.Id;

        var cardVms = new List<ProjectCardViewModel>();
        foreach (var p in allEntries)
        {
            var vm = new ProjectCardViewModel
            {
                Id = p.Id,
                Name = p.Name,
                Path = p.Path,
                Tag = p.Tag,
                CreatedAt = p.CreatedAt,
                EnabledAgents = p.EnabledAgents,
                IsMissing = !Directory.Exists(p.Path),
                IsSelected = p.Id == currentSelectedId
            };

            if (!vm.IsMissing)
            {
                try
                {
                    var file = new VibeTracker.Core.FileEngine(p.Path);
                    if (file.FileExists("state.json"))
                    {
                        var state = file.ReadJson<StateModel>("state.json");
                        vm.Status = state.Status;
                    }
                    var activity = new VibeTracker.Core.ActivityLogger(file);
                    vm.LastActivity = activity.GetLastActivityText();
                }
                catch
                {
                    vm.Status = "unknown";
                }
            }

            cardVms.Add(vm);
        }

        ProjectListItems.ItemsSource = cardVms;
    }

    private void ProjectCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement el && el.DataContext is ProjectCardViewModel card)
        {
            // 取消所有选中
            var cards = ProjectListItems.ItemsSource as List<ProjectCardViewModel>;
            if (cards != null)
                foreach (var c in cards) c.IsSelected = false;

            card.IsSelected = true;

            if (card.IsMissing)
            {
                ShowMissingProjectOptions(card);
                return;
            }

            SelectProject(card);
        }
    }

    // ═══════ 项目选择 & 仪表盘 ═══════

    private void SelectProject(ProjectCardViewModel card)
    {
        try
        {
            _currentDashboard = new DashboardViewModel(card.Path);
            _currentDashboard.Refresh();

            TxtProjectName.Text = card.Name;
            SyncDashboardUI();

            RefreshDashboardLists();

            EmptyPanel.Visibility = Visibility.Collapsed;
            DashboardScroll.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载项目数据失败: {ex.Message}\n\n请确认 .vibe/ 目录结构完整。",
                "加载失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SyncDashboardUI()
    {
        if (_currentDashboard == null) return;

        TxtStatusIcon.Text = _currentDashboard.IsCorrupted ? "⚠️ 数据异常" : _currentDashboard.StatusIcon;
        TxtCurrentTask.Text = _currentDashboard.CurrentTask;
        TxtLastAction.Text = string.IsNullOrWhiteSpace(_currentDashboard.LastAction)
            ? ""
            : _currentDashboard.LastAction;
        TxtLastUpdate.Text = _currentDashboard.LastUpdate;
        TxtAgentActivity.Text = _currentDashboard.AgentActivity;
        TxtProgress.Text = _currentDashboard.ProgressText;
        ProgressBar.Value = _currentDashboard.ProgressPercent;

        if (_currentDashboard.IsCorrupted)
        {
            DataErrorBanner.Visibility = Visibility.Visible;
            TxtDataError.Text = "⚠️ state.json 可能已损坏。请检查 .vibe/ 目录，或按 F2 重新配置项目后让 agent 调用 update_state 恢复。";
        }
        else
        {
            DataErrorBanner.Visibility = Visibility.Collapsed;
        }

        RefreshDashboardLists();
    }

    private void RefreshDashboardLists()
    {
        if (_currentDashboard == null) return;

        // 最近活动
        if (_currentDashboard.RecentLogs.Count > 0)
        {
            RecentLogsList.Visibility = Visibility.Visible;
            TxtNoLogs.Visibility = Visibility.Collapsed;
            RecentLogsList.ItemsSource = null;
            RecentLogsList.ItemsSource = _currentDashboard.RecentLogs;
        }
        else
        {
            RecentLogsList.Visibility = Visibility.Collapsed;
            TxtNoLogs.Visibility = Visibility.Visible;
        }

        // 功能清单
        if (_currentDashboard.Features.Count > 0)
        {
            FeatureChecklist.Visibility = Visibility.Visible;
            TxtNoFeatures.Visibility = Visibility.Collapsed;
            FeatureChecklist.ItemsSource = null;
            FeatureChecklist.ItemsSource = _currentDashboard.Features;
        }
        else
        {
            FeatureChecklist.Visibility = Visibility.Collapsed;
            TxtNoFeatures.Visibility = Visibility.Visible;
        }

        // Bug 追踪
        if (_currentDashboard.Bugs.Count > 0)
        {
            BugsList.Visibility = Visibility.Visible;
            TxtNoBugs.Visibility = Visibility.Collapsed;
            BugsList.ItemsSource = null;
            BugsList.ItemsSource = _currentDashboard.Bugs;
        }
        else
        {
            BugsList.Visibility = Visibility.Collapsed;
            TxtNoBugs.Visibility = Visibility.Visible;
        }

        // 状态变更
        if (_currentDashboard.StatusChanges.Count > 0)
        {
            StatusChangesList.Visibility = Visibility.Visible;
            TxtNoStatusChanges.Visibility = Visibility.Collapsed;
            StatusChangesList.ItemsSource = null;
            StatusChangesList.ItemsSource = _currentDashboard.StatusChanges;
        }
        else
        {
            StatusChangesList.Visibility = Visibility.Collapsed;
            TxtNoStatusChanges.Visibility = Visibility.Visible;
        }

        // 需求/功能变更
        if (_currentDashboard.Changes.Count > 0)
        {
            ChangesList.Visibility = Visibility.Visible;
            TxtNoChanges.Visibility = Visibility.Collapsed;
            ChangesList.ItemsSource = null;
            ChangesList.ItemsSource = _currentDashboard.Changes;
        }
        else
        {
            ChangesList.Visibility = Visibility.Collapsed;
            TxtNoChanges.Visibility = Visibility.Visible;
        }

        if (_currentDashboard.Findings.Count > 0)
        {
            FindingsList.Visibility = Visibility.Visible;
            TxtNoFindings.Visibility = Visibility.Collapsed;
            FindingsList.ItemsSource = null;
            FindingsList.ItemsSource = _currentDashboard.Findings;
        }
        else
        {
            FindingsList.Visibility = Visibility.Collapsed;
            TxtNoFindings.Visibility = Visibility.Visible;
        }

    }

    // ═══════ 路径不存在处理 ═══════

    private void ShowMissingProjectOptions(ProjectCardViewModel card)
    {
        var result = MessageBox.Show(
            $"项目路径不存在：\n{card.Path}\n\n选择操作：\n\n是(Y) — 修改路径\n否(N) — 从列表删除",
            "路径不存在", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);

        switch (result)
        {
            case MessageBoxResult.Yes:
                var dialog = new Views.PathInputDialog(card.Path);
                dialog.Owner = this;
                if (dialog.ShowDialog() == true && Directory.Exists(dialog.NewPath))
                {
                    _indexService.Update(card.Id, p => p.Path = dialog.NewPath);
                    RefreshProjectList();
                }
                else if (!string.IsNullOrWhiteSpace(dialog.NewPath))
                {
                    MessageBox.Show("输入的路径不存在。", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                break;
            case MessageBoxResult.No:
                _indexService.Remove(card.Id);
                RefreshProjectList();
                TxtProjectName.Text = "请选择一个项目";
                _currentDashboard = null;
                EmptyPanel.Visibility = Visibility.Visible;
                DashboardScroll.Visibility = Visibility.Collapsed;
                break;
        }
    }

    // ═══════ 新建项目 / 设置 ═══════

    private void NewProject_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Views.NewProjectDialog(_installPath!, _indexService);
        dialog.Owner = this;
        if (dialog.ShowDialog() == true)
        {
            RefreshProjectList();
        }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        _currentDashboard?.Refresh();
        RefreshProjectList();
        SyncDashboardUI();
        Title = $"VibeTracker — 已刷新 ({DateTime.Now:HH:mm:ss})";
    }

    private void ConfigAgent_Click(object sender, RoutedEventArgs e)
    {
        ReconfigureAgents();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Views.SettingsWindow();
        dialog.Owner = this;
        dialog.ShowDialog();
    }

    // ═══════ 键盘快捷键 ═══════

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.F5)
        {
            _currentDashboard?.Refresh();
            RefreshProjectList();
            SyncDashboardUI();
            Title = $"VibeTracker — 已刷新 ({DateTime.Now:HH:mm:ss})";
        }
        else if (e.Key == Key.F2 && _currentDashboard != null)
        {
            // F2: 重新配置当前项目的 agent
            ReconfigureAgents();
        }
        base.OnKeyDown(e);
    }

    // ═══════ Agent 重新配置 ═══════

    private void ReconfigureAgents()
    {
        if (_currentDashboard == null)
        {
            MessageBox.Show("请先选择一个项目。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // 从索引中找到当前项目
        var projects = _indexService.Load();
        var current = projects.FirstOrDefault(p =>
        {
            var cards = ProjectListItems.ItemsSource as List<ProjectCardViewModel>;
            var selected = cards?.FirstOrDefault(c => c.IsSelected);
            return selected != null && p.Id == selected.Id;
        });

        if (current == null)
        {
            MessageBox.Show("未在索引中找到当前项目。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var summary = "重新配置 Agent 连接：\n\n";
        var configService = new AgentConfigService(_installPath!, current.Path, current.Id);

        if (current.EnabledAgents.Contains("claude"))
        {
            var (_, msg) = configService.ConfigureClaudeDesktop();
            summary += $"Claude Desktop: {msg}\n\n";
        }
        if (current.EnabledAgents.Contains("codex"))
        {
            var (_, msg) = configService.ConfigureCodexDesktop();
            summary += $"Codex Desktop: {msg}\n\n";
        }
        if (current.EnabledAgents.Contains("claude-code"))
        {
            var (_, msg) = configService.ConfigureClaudeCode();
            summary += $"Claude Code: {msg}\n\n";
        }

        summary += "请重启对应桌面端使配置生效。";
        MessageBox.Show(summary, "Agent 配置", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
