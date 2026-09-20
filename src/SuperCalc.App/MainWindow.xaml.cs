using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SuperCalc.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.System;

namespace SuperCalc.App;

public sealed partial class MainWindow : Window
{
    private readonly StateStore store;
    private readonly string? smokeDirectory;
    private readonly AppState state;
    private bool ready, busy, dialogOpen, allowClose, hasResult, isClosed;
    private bool assistantRequested = true;
    private decimal lastResult;
    private string currentPage = "calc";
    private bool Satire => !state.FocusMode;
    private bool Drama => Satire && state.DramaticMode;

    public MainWindow(string? smokeDirectory = null)
    {
        this.smokeDirectory = smokeDirectory;
        store = new StateStore(smokeDirectory is null
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SuperCalc", "state.json")
            : Path.Combine(smokeDirectory, "state.json"));
        state = smokeDirectory is null ? store.Load() : new AppState();
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBar);
        AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "SuperCalc.ico"));
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        AppWindow.Resize(new SizeInt32(Math.Min(1000, area.Width - 60), Math.Min(760, area.Height - 50)));
        AppWindow.Move(new PointInt32(area.X + (area.Width - AppWindow.Size.Width) / 2, area.Y + (area.Height - AppWindow.Size.Height) / 2));
        AppWindow.Closing += Window_Closing;
        Closed += (_, _) => isClosed = true;
        FocusToggle.IsOn = state.FocusMode;
        DramaToggle.IsOn = state.DramaticMode;
        AnimationToggle.IsOn = state.Animations;
        RecommendationsToggle.IsOn = state.Recommendations;
        ExitToggle.IsOn = state.ExitSurvey;
        Root.Loaded += Root_Loaded;
        Root.SizeChanged += (_, _) => ApplyMode();
        BuildMuseum();
        InitializeMotion();
        Activated += (_, e) => TitleBar.Opacity = e.WindowActivationState == WindowActivationState.Deactivated ? 0.55 : 1;
        ready = true;
        ApplyMode();
    }

    private async void Root_Loaded(object sender, RoutedEventArgs e)
    {
        var scale = Root.XamlRoot.RasterizationScale;
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        AppWindow.Resize(new SizeInt32(Math.Min((int)(1000 * scale), area.Width - 40), Math.Min((int)(760 * scale), area.Height - 40)));
        AppWindow.Move(new PointInt32(area.X + (area.Width - AppWindow.Size.Width) / 2, area.Y + (area.Height - AppWindow.Size.Height) / 2));
        if (smokeDirectory is not null) { await RunSmokeTest(smokeDirectory); return; }
        if (store.LastError is not null) Notify("本地存储", store.LastError, InfoBarSeverity.Warning);
        if (!state.Onboarded && !state.FocusMode) await Onboard();
        if (!isClosed) ExpressionBox.Focus(FocusState.Programmatic);
    }

    private void Save()
    {
        if (!store.Save(state)) Notify("保存失败", store.LastError!, InfoBarSeverity.Warning);
        RefreshStats();
    }

    private void RefreshStats()
    {
        if (isClosed) return;
        SessionStats.Text = $"{state.Calculations} 次计算  ·  {state.History.Count} 条历史记录";
        var score = Math.Min(100, 42 + (state.LocalPersona ? 18 : 0) + (state.PremiumPretend ? 25 : 0) + Math.Min(15, state.Ceremonies));
        HealthScore.Text = $"{score} / 100";
        HealthProgress.Value = score;
        AccountLabel.Text = state.LocalPersona ? "SuperCalc 用户" : "个人资料";
        MemoryLabel.Text = state.Memory == 0 ? "M · 空" : "M · " + Calculator.Format(state.Memory);
    }

    private void ApplyMode()
    {
        if (!ready || isClosed) return;
        if (suggestionNotice && (!Drama || !state.Recommendations)) { Notice.IsOpen = false; Notice.ActionButton = null; suggestionNotice = false; }
        Navigation.IsPaneVisible = !state.FocusMode;
        var showAssistant = Satire && assistantRequested && Root.ActualWidth >= 720;
        AssistantPanel.Visibility = showAssistant ? Visibility.Visible : Visibility.Collapsed;
        AssistantColumn.Width = new GridLength(showAssistant ? 300 : 0);
        var hasHeight = Root.ActualHeight >= 650;
        DisplayRow.Height = new GridLength(hasHeight ? 150 : 124);
        ResultText.FontSize = hasHeight ? 56 : 40;
        foreach (var key in Keypad.Children.OfType<Button>())
        {
            if (hasHeight) key.ClearValue(Control.FontSizeProperty);
            else key.FontSize = 20;
            key.Padding = new Thickness(hasHeight ? 4 : 0);
        }
        WelcomeBanner.Visibility = Satire && hasHeight ? Visibility.Visible : Visibility.Collapsed;
        RecommendationCard.Visibility = Satire && state.Recommendations && hasHeight ? Visibility.Visible : Visibility.Collapsed;
        SearchPromotion.Visibility = Satire && state.Recommendations ? Visibility.Visible : Visibility.Collapsed;
        if (state.FocusMode && hasResult) ResultCaption.Text = "计算完成";
        StatusText.Text = state.FocusMode ? "专注模式" : "就绪";
        RefreshStats();
    }

    private void Focus_Toggled(object sender, RoutedEventArgs e)
    {
        if (!ready) return;
        state.FocusMode = FocusToggle.IsOn;
        if (state.FocusMode) Navigate("calc");
        ApplyMode(); Save();
    }

    private void Settings_Toggled(object sender, RoutedEventArgs e)
    {
        if (!ready) return;
        state.DramaticMode = DramaToggle.IsOn;
        state.Animations = AnimationToggle.IsOn;
        if (!state.Animations) ResetMotion();
        state.Recommendations = RecommendationsToggle.IsOn;
        state.ExitSurvey = ExitToggle.IsOn;
        ApplyMode(); Save();
    }

    private void Navigation_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (!ready) return;
        if (args.IsSettingsInvoked) { Navigate("settings"); return; }
        switch (args.InvokedItemContainer?.Tag as string)
        {
            case "cloud": Cloud_Click(sender, new RoutedEventArgs()); break;
            case "premium": Premium_Click(sender, new RoutedEventArgs()); break;
            case "account": Account_Click(sender, new RoutedEventArgs()); break;
            case string page: Navigate(page); break;
        }
        Navigation.IsPaneOpen = false;
    }
    private void History_Click(object sender, RoutedEventArgs e) => Navigate("history");
    private void SideTabs_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (ready) RenderRecent(); }
    private async void Panel_Click(object sender, RoutedEventArgs e)
    {
        if (Root.ActualWidth < 720)
        {
            if (await Dialog("CalcPilot", PilotReply.Text, "解释当前结果") == ContentDialogResult.Primary)
            {
                await RunPilot();
                if (!isClosed) await Dialog("CalcPilot", PilotReply.Text, "完成");
            }
            return;
        }
        assistantRequested = state.FocusMode || !assistantRequested;
        if (state.FocusMode) FocusToggle.IsOn = false;
        ApplyMode();
        if (assistantRequested) AnimateEntrance(AssistantPanel);
    }
    private void Navigate(string page)
    {
        currentPage = page;
        CalculatorPage.Visibility = page == "calc" ? Visibility.Visible : Visibility.Collapsed;
        HistoryPage.Visibility = page == "history" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = page == "settings" ? Visibility.Visible : Visibility.Collapsed;
        MuseumPage.Visibility = page == "museum" ? Visibility.Visible : Visibility.Collapsed;
        PageTitle.Text = page switch { "history" => "历史记录", "settings" => "设置", "museum" => "新增功能", _ => "标准" };
        Navigation.SelectedItem = page switch { "history" => HistoryNav, "museum" => MuseumNav, "settings" => Navigation.SettingsItem, _ => CalcNav };
        if (page == "history") RenderHistory();
        ApplyMode();
        AnimateEntrance(page switch { "history" => HistoryPage, "settings" => SettingsPage, "museum" => MuseumPage, _ => CalculatorPage });
    }

    private void Insert(string text)
    {
        if (busy) return;
        if (hasResult)
        {
            ExpressionBox.Text = text is "+" or "−" or "-" or "×" or "÷" or "*" or "/" or "^" or "%" ? Calculator.Format(lastResult) : "";
            ExpressionBox.SelectionStart = ExpressionBox.Text.Length;
            hasResult = false;
        }
        var start = ExpressionBox.SelectionStart;
        var current = ExpressionBox.Text;
        var result = current.Remove(start, ExpressionBox.SelectionLength).Insert(start, text);
        if (result.Length > 512) { Notify("算式太长", "最多支持 512 个字符。", InfoBarSeverity.Warning); return; }
        ExpressionBox.Text = result;
        ExpressionBox.SelectionStart = start + text.Length;
        ExpressionBox.Focus(FocusState.Programmatic);
    }

    private void Key_Click(object sender, RoutedEventArgs e) => Insert((string)((Button)sender).Tag);
    private void Expression_Changed(object sender, TextChangedEventArgs e) { hasResult = false; }
    private async void Equals_Click(object sender, RoutedEventArgs e) => await Calculate();

    private async Task Calculate()
    {
        if (busy || dialogOpen) return;
        var expression = ExpressionBox.Text;
        busy = true;
        EqualsButton.IsEnabled = false;
        ExpressionBox.IsReadOnly = true;
        try
        {
            var value = Calculator.Evaluate(expression);
            if (!await PrepareCalculation(expression)) { if (!isClosed) ResultCaption.Text = "本次计算已取消"; return; }
            if (isClosed) return;
            lastResult = value;
            hasResult = true;
            ResultText.Text = Calculator.Format(value);
            ResultCaption.Text = "计算完成";
            AnimateEntrance(ResultText);
            state.Record(expression, value);
            RenderRecent();
            if (Satire)
            {
                PilotReply.Text = $"结果为 {Calculator.Format(value)}。需要我为你提供进一步的分析或建议吗？";
                RecommendationText.Text = $"喜欢 {Calculator.Format(value)}？你可能也会喜欢 SuperCalc 365。";
                OfferNextStep();
                if (Drama && state.ResultExperienceChosen && state.OpenResultsWithAssistant)
                { assistantRequested = true; SideTabs.SelectedIndex = 0; PilotReply.Text = $"当前结果为 {Calculator.Format(value)}。你可以继续计算，或选择下方操作获取更多建议。"; }
            }
            Save();
        }
        catch (Exception ex) when (ex is FormatException or DivideByZeroException or OverflowException)
        {
            hasResult = false;
            ResultText.Text = "—";
            ResultCaption.Text = "请修改算式后重试";
            var message = ex is OverflowException ? "结果超出 decimal 数值范围（约 29 位有效数字）。" : ex.Message;
            Notify(Drama ? "出现了一些问题，但我们正在改善体验" : "无法计算", message, InfoBarSeverity.Error);
            if (Drama) PilotReply.Text = "检查算式并重试，或使用 CalcPilot 获取更多帮助。";
            AnimateError();
        }
        finally
        {
            busy = false;
            if (!isClosed)
            {
                EqualsButton.IsEnabled = true;
                ExpressionBox.IsReadOnly = false;
                ThinkingBar.Visibility = Visibility.Collapsed;
                ApplyMode();
                ExpressionBox.Focus(FocusState.Programmatic);
                ExpressionBox.SelectAll();
            }
        }
    }

    private async void Expression_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter) { e.Handled = true; await Calculate(); }
        else if (e.Key == VirtualKey.Escape) { e.Handled = true; Clear(); }
        else if (hasResult && e.Key is VirtualKey.Add or VirtualKey.Subtract or VirtualKey.Multiply or VirtualKey.Divide)
        {
            e.Handled = true;
            Insert(e.Key switch { VirtualKey.Add => "+", VirtualKey.Subtract => "−", VirtualKey.Multiply => "×", _ => "÷" });
        }
    }
    private void Root_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape && !dialogOpen && currentPage == "calc") { Clear(); e.Handled = true; }
    }
    private void Clear_Click(object sender, RoutedEventArgs e) => Clear();
    private void Clear()
    {
        if (busy) return;
        ExpressionBox.Text = ""; ResultText.Text = "0"; lastResult = 0; hasResult = false;
        ResultCaption.Text = "就绪";
        Notice.IsOpen = false;
        ExpressionBox.Focus(FocusState.Programmatic);
    }
    private void ClearEntry_Click(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        ExpressionBox.Text = Regex.Replace(ExpressionBox.Text, @"\d*\.?\d+\s*$", "");
        ExpressionBox.SelectionStart = ExpressionBox.Text.Length;
        ExpressionBox.Focus(FocusState.Programmatic);
    }
    private void Backspace_Click(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        var start = ExpressionBox.SelectionStart;
        var length = ExpressionBox.SelectionLength;
        if (length > 0) ExpressionBox.Text = ExpressionBox.Text.Remove(start, length);
        else if (start > 0) { ExpressionBox.Text = ExpressionBox.Text.Remove(start - 1, 1); start--; }
        ExpressionBox.SelectionStart = start;
        ExpressionBox.Focus(FocusState.Programmatic);
    }
    private void Unary_Click(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        var expression = string.IsNullOrWhiteSpace(ExpressionBox.Text) ? Calculator.Format(lastResult) : ExpressionBox.Text;
        ExpressionBox.Text = ((Button)sender).Tag switch
        {
            "square" => $"({expression})^2", "sqrt" => $"sqrt({expression})", "reciprocal" => $"1/({expression})", _ => $"-({expression})"
        };
        ExpressionBox.SelectionStart = ExpressionBox.Text.Length;
        ExpressionBox.Focus(FocusState.Programmatic);
    }

    private void Memory_Click(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        try
        {
            switch (((Button)sender).Tag)
            {
                case "clear": state.Memory = 0; break;
                case "recall": Insert(Calculator.Format(state.Memory)); break;
                case "add": state.Memory = checked(state.Memory + Calculator.Evaluate(string.IsNullOrWhiteSpace(ExpressionBox.Text) ? Calculator.Format(lastResult) : ExpressionBox.Text)); break;
                case "subtract": state.Memory = checked(state.Memory - Calculator.Evaluate(string.IsNullOrWhiteSpace(ExpressionBox.Text) ? Calculator.Format(lastResult) : ExpressionBox.Text)); break;
            }
            Save();
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or DivideByZeroException) { Notify("记忆操作失败", ex.Message, InfoBarSeverity.Warning); }
    }

    private void More_Click(object sender, RoutedEventArgs e)
    {
        var menu = new MenuFlyout();
        AddMenu(menu, "计算器", () => Navigate("calc"));
        AddMenu(menu, "计算时间线", () => Navigate("history"));
        AddMenu(menu, "体验设置", () => Navigate("settings"));
        AddMenu(menu, "新增功能", () => Navigate("museum"));
        menu.Items.Add(new MenuFlyoutSeparator());
        AddMenu(menu, "插入左括号 (", () => Insert("("));
        AddMenu(menu, "插入右括号 )", () => Insert(")"));
        AddMenu(menu, "复制结果", () => Copy_Click(sender, e));
        AddMenu(menu, Drama ? "显示更多选项…" : "传统数字控制面板", () => Legacy_Click(sender, e));
        menu.ShowAt((FrameworkElement)sender);
    }
    private static void AddMenu(MenuFlyout menu, string label, Action action)
    {
        var item = new MenuFlyoutItem { Text = label };
        item.Click += (_, _) => action();
        menu.Items.Add(item);
    }

    private async void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        if (ResultText.Text == "—") { Notify("没有可复制的结果", "先完成一次有效计算。", InfoBarSeverity.Warning); return; }
        if (Drama)
        {
            state.Ceremonies++;
            var choice = await Dialog("分享计算结果", "使用 OneNumber 管理你的数字工作空间。\n\n若要使用其他应用，请选择“显示更多选项”。", "显示更多选项", "取消", "保存到 OneNumber");
            if (choice == ContentDialogResult.Secondary) { Cloud_Click(sender, e); return; }
            if (choice != ContentDialogResult.Primary) return;
            if (await Dialog("剪贴板属性", "常规  |  详细信息\n\n对象类型：数值\n格式：Unicode 文本\n目标：Windows 剪贴板", "复制", "取消") != ContentDialogResult.Primary) return;
        }
        try
        {
            var data = new DataPackage(); data.SetText(ResultText.Text); Clipboard.SetContent(data);
            Notify("已复制", "结果已复制到剪贴板。", InfoBarSeverity.Success);
            Save();
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException) { Notify("剪贴板暂不可用", "请稍后再试。", InfoBarSeverity.Warning); }
    }

    private Task<ContentDialogResult> Dialog(string title, object content, string primary, string close = "关闭", string? secondary = null)
        => ShowExperienceDialog(CreateExperienceDialog(title, content, primary, close, secondary));
    private async void Onboarding_Click(object sender, RoutedEventArgs e) => await Onboard();

    private async void Account_Click(object sender, RoutedEventArgs e)
    {
        state.Ceremonies++;
        if (await Dialog("一个资料，更多可能", "SuperCalc 个人资料\n\n• 将你的计算与偏好集中在一处\n• 获取更适合你的工作流建议\n• 完成体验健康度检查\n\n选择继续以初始化此设备上的个人资料。", "继续", "稍后提醒我") == ContentDialogResult.Primary)
        { state.LocalPersona = true; Notify("个人资料已就绪", "你可以在设置中管理自己的偏好。", InfoBarSeverity.Success); }
        Save();
    }
    private async void Premium_Click(object sender, RoutedEventArgs e)
    {
        state.Ceremonies++;
        if (await Dialog("使用 SuperCalc 365 做到更多", new Controls.PremiumOffer(), "开始使用", "暂时跳过") == ContentDialogResult.Primary)
        { state.PremiumPretend = true; Notify("欢迎使用 SuperCalc 365", "你的预览权益已启用。", InfoBarSeverity.Success); }
        Save();
    }
    private async void Cloud_Click(object sender, RoutedEventArgs e)
    {
        state.Ceremonies++;
        var choice = await Dialog("OneNumber 需要你的关注", $"工作空间中有 {state.History.Count} 条记录。\n\n发现名称相同的项目，请选择要保留的版本。\n\n此设备：2\n其他版本：2", "保留两个副本", "稍后处理");
        if (choice == ContentDialogResult.Primary) Notify("已保留两个副本", "你可以继续使用工作空间。", InfoBarSeverity.Success);
        Save();
    }
    private async void Pilot_Click(object sender, RoutedEventArgs e) => await RunPilot();
    private async Task RunPilot()
    {
        if (busy || dialogOpen) return;
        busy = true;
        state.Ceremonies++;
        ThinkingBar.Visibility = Visibility.Visible;
        PilotReply.Text = "正在从战略层面理解您的等号…";
        if (state.Animations && Satire) await Task.Delay(650);
        if (isClosed) return;
        PilotReply.Text = ToneBox.SelectedIndex switch
        {
            1 => $"你做到了！{ResultText.Text} 不只是一个数字，更是你迈向无限可能的一小步。",
            2 => $"建议围绕 {ResultText.Text} 建立数字化闭环，通过等号赋能，以圆角为抓手，持续对齐计算生态。",
            _ => $"当前计算结果为 {ResultText.Text}。你可以将其用于后续计算，或保存到工作空间以便稍后继续。"
        };
        ThinkingBar.Visibility = Visibility.Collapsed; AnimateEntrance(PilotReply); busy = false; Save();
    }
    private void Recommendation_Click(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        Navigate("calc"); ExpressionBox.Text = "365 × 0";
        ExpressionBox.Focus(FocusState.Programmatic);
        Notify("已为你准备算式", "按 Enter 查看结果。");
    }
    private async void Optimize_Click(object sender, RoutedEventArgs e)
    {
        state.Ceremonies++;
        var rating = new RatingControl { Caption = "请评价本次等号", MaxRating = 5, Value = 5 };
        var panel = new StackPanel { Spacing = 16 };
        panel.Children.Add(new TextBlock { Text = "你对最近的计算体验满意吗？请帮助我们持续改进。", TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(rating);
        if (await Dialog("你的反馈对我们至关重要", panel, "提交反馈", "以后再说") == ContentDialogResult.Primary)
        { HealthText.Text = $"感谢你的 {rating.Value:0} 星评价。"; Notify("感谢反馈", "你的意见有助于改善 SuperCalc。", InfoBarSeverity.Success); }
        Save();
    }
    private async void Legacy_Click(object sender, RoutedEventArgs e)
    {
        state.Ceremonies++;
        var panel = new StackPanel { Spacing = 12, Background = new SolidColorBrush(ColorHelper.FromArgb(255, 236, 233, 216)), Padding = new Thickness(16) };
        panel.Children.Add(new TextBlock { Text = "数字属性   |   常规   |   高级", FontFamily = new FontFamily("Tahoma"), Foreground = new SolidColorBrush(Colors.Black) });
        panel.Children.Add(new TextBlock { Text = "此设备工作正常。\n\n计算引擎：Decimal\n精度：约 29 位有效数字\n百分数：x% = x / 100\n平方根：使用浮点近似\n\n部分选项由系统管理。", TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Colors.Black) });
        panel.Children.Add(new CheckBox { Content = "启用兼容性优化", IsChecked = true, IsEnabled = false });
        await Dialog("高级数字属性（传统）", panel, "确定", "取消", "应用"); Save();
    }
    private async void Update_Click(object sender, RoutedEventArgs e)
    {
        if (busy || dialogOpen) return;
        state.Ceremonies++;
        var choice = await Dialog("体验更新已就绪", "SuperCalc 体验包 KB000042\n\n• 优化结果显示和控件响应\n• 改善工作空间体验\n• 更新推荐设置\n\n应用更新后，你可以继续计算。", "立即应用", "稍后提醒我");
        if (choice == ContentDialogResult.Primary)
        {
            busy = true;
            StatusText.Text = "正在应用体验更新… 99%";
            if (state.Animations && Satire) await Task.Delay(750);
            if (isClosed) return;
            busy = false;
            Notify("你已准备就绪", "已应用最新的体验设置。", InfoBarSeverity.Success);
        }
        Save(); ApplyMode();
    }

    private void Search_Changed(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args) { if (ready) RenderHistory(); }
    private void RenderHistory()
    {
        HistoryItems.Children.Clear();
        var query = HistorySearch.Text.Trim();
        var matches = state.History.Where(x => x.Expression.Contains(query, StringComparison.OrdinalIgnoreCase) || x.Result.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count == 0) HistoryItems.Children.Add(new TextBlock { Text = state.History.Count == 0 ? "这里还没有计算。你的下一次等号，将开启时间线。" : "没有匹配的本地结果。试试其他数字。", Margin = new Thickness(4, 24, 4, 24), TextWrapping = TextWrapping.Wrap });
        foreach (var entry in matches)
        {
            var row = new Grid { ColumnSpacing = 16 };
            row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var texts = new StackPanel { Spacing = 6 };
            texts.Children.Add(new TextBlock { Text = entry.Expression + " = " + entry.Result, FontSize = 18, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true });
            texts.Children.Add(new TextBlock { Text = entry.Timestamp.ToLocalTime().ToString("MM/dd HH:mm", CultureInfo.InvariantCulture) + "  ·  本地保存成功", FontSize = 11, Foreground = (Brush)Application.Current.Resources["MutedBrush"] });
            row.Children.Add(texts);
            var button = new Button { Content = "再次体验", VerticalAlignment = VerticalAlignment.Center };
            button.Click += (_, _) => { if (busy) return; Navigate("calc"); ExpressionBox.Text = entry.Expression; ExpressionBox.Focus(FocusState.Programmatic); ExpressionBox.SelectionStart = ExpressionBox.Text.Length; };
            Grid.SetColumn(button, 1); row.Children.Add(button);
            HistoryItems.Children.Add(new Border { Style = (Style)Application.Current.Resources["Card"], Child = row });
        }
    }
    private async void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        if (await Dialog("清空本机计算历史？", "最多 100 条历史记录将从此设备移除。此操作无法撤销。", "清空历史", "保留") != ContentDialogResult.Primary) return;
        state.History.Clear(); Save(); RenderHistory(); RenderRecent();
    }

    private void BuildMuseum()
    {
        var exhibits = new[]
        {
            ("为你量身定制", "完成设备设置，获取更适合自己的计算体验。"),
            ("全新的工作空间", "使用紧凑导航快速访问常用工具和服务。"),
            ("认识 CalcPilot", "在当前算式旁获取解释、建议和下一步灵感。"),
            ("更流畅的计算", "全新的过渡动画，让每一步操作都有清晰反馈。"),
            ("分享你的结果", "通过 OneNumber 或其他应用继续你的工作。"),
            ("从上次离开的地方继续", "快速搜索最近的计算，并将算式重新带回工作区。"),
            ("按你的方式工作", "在设置中调整建议、动画和增强计算体验。"),
            ("保持最新", "获取最新体验设置，持续改善日常工作流。"),
            ("你的意见很重要", "告诉我们哪些功能对你最有帮助。"),
            ("专注当下", "开启专注模式，为当前计算保留更多空间。")
        };
        foreach (var (title, body) in exhibits)
        {
            var panel = new StackPanel { Spacing = 8 };
            panel.Children.Add(new TextBlock { Text = title, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 16 });
            panel.Children.Add(new TextBlock { Text = body, TextWrapping = TextWrapping.Wrap, Foreground = (Brush)Application.Current.Resources["MutedBrush"] });
            MuseumItems.Children.Add(new Border { Style = (Style)Application.Current.Resources["Card"], Child = panel });
        }
    }

    private void Notify(string title, string message, InfoBarSeverity severity = InfoBarSeverity.Informational)
    { if (isClosed) return; suggestionNotice = false; Notice.ActionButton = null; Notice.Title = title; Notice.Message = message; Notice.Severity = severity; Notice.IsOpen = true; AnimateEntrance(Notice); }

    private void CloseSafely()
    {
        // Window.Close() doesn't raise AppWindow.Closing, so the programmatic path needs this too.
        FocusToggle.Focus(FocusState.Programmatic);
        ExpressionBox.IsEnabled = false;
        HistorySearch.IsEnabled = false;
        DispatcherQueue.TryEnqueue(() => Close());
    }

    private async void Window_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        // WinUI issue #11653: don't tear down the input services with a TextBox focused.
        FocusToggle.Focus(FocusState.Programmatic);
        if (allowClose) return;
        Save();
        // An existing modal must never trap the user in the application.
        if (!Satire || !state.ExitSurvey || dialogOpen) return;
        args.Cancel = true;
        var choice = await Dialog("离开之前，帮助我们改善体验", $"你已完成 {state.Calculations} 次计算。\n\n花一点时间评价 SuperCalc，帮助我们为你提供更好的服务。", "直接退出", "继续计算", "提供反馈");
        if (choice == ContentDialogResult.Primary)
        {
            allowClose = true;
            CloseSafely();
        }
        else if (choice == ContentDialogResult.Secondary) Optimize_Click(this, new RoutedEventArgs());
    }
}
