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
        AppWindow.Resize(new SizeInt32(Math.Min(1380, area.Width - 60), Math.Min(1000, area.Height - 50)));
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
        ready = true;
        ApplyMode();
    }

    private async void Root_Loaded(object sender, RoutedEventArgs e)
    {
        var scale = Root.XamlRoot.RasterizationScale;
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        AppWindow.Resize(new SizeInt32(Math.Min((int)(1380 * scale), area.Width - 40), Math.Min((int)(1000 * scale), area.Height - 40)));
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
        SessionStats.Text = $"{state.Calculations} 次计算  /  {state.Ceremonies} 次体验  /  100% 本地";
        var score = Math.Min(100, 42 + (state.LocalPersona ? 18 : 0) + (state.PremiumPretend ? 25 : 0) + Math.Min(15, state.Ceremonies));
        HealthScore.Text = $"{score} / 100";
        HealthProgress.Value = score;
        AccountLabel.Text = state.LocalPersona ? "尊贵的本地体验者" : "本地体验者";
        MemoryLabel.Text = state.Memory == 0 ? "M · 空" : "M · " + Calculator.Format(state.Memory);
    }

    private void ApplyMode()
    {
        if (!ready || isClosed) return;
        var showSide = !state.FocusMode && Root.ActualWidth >= 800;
        Sidebar.Visibility = showSide ? Visibility.Visible : Visibility.Collapsed;
        CompactNavigation.Visibility = showSide ? Visibility.Collapsed : Visibility.Visible;
        NavColumn.Width = new GridLength(showSide ? 224 : 0);
        var showAssistant = Satire && Root.ActualWidth >= 1130;
        CalculatorPage.MaxWidth = state.FocusMode ? 640 : double.PositiveInfinity;
        CalculatorPage.HorizontalAlignment = state.FocusMode ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
        AssistantPanel.Visibility = showAssistant ? Visibility.Visible : Visibility.Collapsed;
        AssistantColumn.Width = new GridLength(showAssistant ? 296 : 0);
        WelcomeBanner.Visibility = Satire ? Visibility.Visible : Visibility.Collapsed;
        RecommendationCard.Visibility = Satire && state.Recommendations ? Visibility.Visible : Visibility.Collapsed;
        SearchPromotion.Visibility = Satire && state.Recommendations ? Visibility.Visible : Visibility.Collapsed;
        PreviewBadge.Visibility = Satire ? Visibility.Visible : Visibility.Collapsed;
        KeypadTip.Visibility = Satire ? Visibility.Visible : Visibility.Collapsed;
        if (state.FocusMode && hasResult) ResultCaption.Text = "计算完成";
        PageSubtitle.Text = state.FocusMode ? "直接输入，直接得到答案。" : currentPage switch
        {
            "history" => "每一次等号，都值得被记住。",
            "settings" => "你的偏好很重要，所以我们把它放在这里。",
            "museum" => "从真实槽点，到过量体验。",
            _ => "一个简单的答案，值得一整套生态。"
        };
        StatusText.Text = state.FocusMode ? "● 专注模式 · 无推荐、无等待、无挽留" : "● 所有系统都在为一个等号努力";
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
        state.Recommendations = RecommendationsToggle.IsOn;
        state.ExitSurvey = ExitToggle.IsOn;
        ApplyMode(); Save();
    }

    private void Navigate_Click(object sender, RoutedEventArgs e) => Navigate((string)((Button)sender).Tag);
    private void Navigate(string page)
    {
        currentPage = page;
        CalculatorPage.Visibility = page == "calc" ? Visibility.Visible : Visibility.Collapsed;
        HistoryPage.Visibility = page == "history" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = page == "settings" ? Visibility.Visible : Visibility.Collapsed;
        MuseumPage.Visibility = page == "museum" ? Visibility.Visible : Visibility.Collapsed;
        PageTitle.Text = page switch { "history" => "计算时间线", "settings" => "体验设置", "museum" => "设计博物馆", _ => "计算器" };
        foreach (var (button, tag) in new[] { (CalcNav, "calc"), (HistoryNav, "history"), (SettingsNav, "settings"), (MuseumNav, "museum") })
            button.Background = new SolidColorBrush(tag == page ? ColorHelper.FromArgb(255, 222, 222, 247) : Colors.Transparent);
        if (page == "history") RenderHistory();
        ApplyMode();
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
            if (Drama && state.Animations)
            {
                state.Ceremonies++;
                ThinkingBar.Visibility = Visibility.Visible;
                foreach (var message in new[] { "正在准备您的数字工作空间…", "正在为结果应用圆角…", "正在确认 1 + 1 的兼容性…" })
                {
                    if (state.FocusMode) break;
                    StatusText.Text = message;
                    await Task.Delay(220);
                    if (isClosed) return;
                }
            }
            lastResult = value;
            hasResult = true;
            ResultText.Text = Calculator.Format(value);
            ResultCaption.Text = Satire ? "计算已完成。体验才刚刚开始。" : "计算完成";
            state.Record(expression, value);
            if (Satire)
            {
                PilotReply.Text = $"我注意到你得到了 {Calculator.Format(value)}。要不要把它变成一次战略机遇？";
                RecommendationText.Text = $"喜欢 {Calculator.Format(value)}？你可能也会喜欢 SuperCalc 365。";
                if (Drama && state.Calculations % 3 == 0)
                    Notify("帮助我们变得更好", "你刚完成一次计算。欢迎在“优化我的体验”里为这次等号评分。");
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
            if (Drama) PilotReply.Text = "建议：重新输入算式。购买更多圆角并不能修复数学错误。";
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
        ResultCaption.Text = Satire ? "清空的是数字，不是可能性。" : "准备就绪";
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
        AddMenu(menu, "设计博物馆", () => Navigate("museum"));
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
            var choice = await Dialog("分享您的生产力", "推荐：将数字保存到 OneNumber，开启跨设备想象。\n\n只想复制？经典命令已迁移到“显示更多选项”。", "显示更多选项", "取消", "模拟云同步");
            if (choice == ContentDialogResult.Secondary) { Cloud_Click(sender, e); return; }
            if (choice != ContentDialogResult.Primary) return;
            if (await Dialog("传统剪贴板属性", "剪贴板版本：1995（精神上）\n对象类型：一个数字\n\n已找到您从一开始就想使用的命令。", "复制结果", "取消") != ContentDialogResult.Primary) return;
        }
        try
        {
            var data = new DataPackage(); data.SetText(ResultText.Text); Clipboard.SetContent(data);
            Notify("已复制", Satire ? "经历两代界面，数字终于抵达剪贴板。" : "结果已复制到剪贴板。");
            Save();
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException) { Notify("剪贴板暂不可用", "请稍后再试。", InfoBarSeverity.Warning); }
    }

    private async Task<ContentDialogResult> Dialog(string title, object content, string primary, string close = "关闭", string? secondary = null)
    {
        if (dialogOpen) return ContentDialogResult.None;
        dialogOpen = true;
        try
        {
            var dialog = new ContentDialog
            {
                XamlRoot = Root.XamlRoot, RequestedTheme = ElementTheme.Light, Title = title,
                Content = content is string text ? new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, LineHeight = 24, MaxWidth = 440 } : content,
                PrimaryButtonText = primary, CloseButtonText = close, SecondaryButtonText = secondary ?? "",
                DefaultButton = ContentDialogButton.Primary
            };
            return await dialog.ShowAsync();
        }
        finally { dialogOpen = false; }
    }

    private async Task Onboard()
    {
        if (dialogOpen) return;
        var steps = new[]
        {
            ("让我们完成计算器的设置", "1 / 3   欢迎来到数字生活的新篇章\n\n在您计算 2 + 2 之前，我们希望先了解您的梦想。\n\n这是独立讽刺应用。所有账号、云、订阅与 AI 只在本地演出。"),
            ("为每一个数字找到归属", "2 / 3   您的数字值得一个账户\n\n登录可以同步您的愿景，备份您的可能性，并让 7 在所有设备上仍然是 7。\n\n此处没有真实登录，也不索取任何个人信息。"),
            ("使用推荐设置，释放您的潜力", "3 / 3   我们已为您推荐所有推荐\n\n✓ 个性化数字灵感\n✓ CalcPilot 深度仪式\n✓ 体验健康度与退出关怀\n\n随时打开“让我算数”，恢复清静。")
        };
        foreach (var (title, body) in steps)
        {
            state.Ceremonies++;
            if (await Dialog(title, body, "接受并继续", "跳过，直接计算") != ContentDialogResult.Primary) break;
        }
        state.Onboarded = true; Save();
    }
    private async void Onboarding_Click(object sender, RoutedEventArgs e) => await Onboard();

    private async void Account_Click(object sender, RoutedEventArgs e)
    {
        state.Ceremonies++;
        if (await Dialog("一个账户，连接所有小数点", "账户权益（本地模拟）\n\n• 数字跨设备漫游：在想象中已同步\n• 个人化称呼：尊贵的本地体验者\n• 体验健康度：立即 +18\n\n不需要邮箱、密码或互联网。", "创建本地体验身份", "暂时保持不完整") == ContentDialogResult.Primary)
        { state.LocalPersona = true; Notify("身份已升级", "欢迎，尊贵的本地体验者。您的数据仍只在本机。"); }
        Save();
    }
    private async void Premium_Click(object sender, RoutedEventArgs e)
    {
        state.Ceremonies++;
        if (await Dialog("SuperCalc 365 · 为可能性付费", "免费版：无限次正确计算\n\n想象版：¥ 0 / 永久\n✓ 365 个不存在的增值权益\n✓ 云端小数点与企业级等号\n✓ 尊贵体验徽章\n\n这是一张讽刺套餐卡，不会发生购买。", "免费领取虚构权益", "继续免费算数") == ContentDialogResult.Primary)
        { state.PremiumPretend = true; Notify("想象版已激活", "您已拥有全部虚构权益。计算精度没有变化。"); }
        Save();
    }
    private async void Cloud_Click(object sender, RoutedEventArgs e)
    {
        state.Ceremonies++;
        await Dialog("OneNumber · 数字有了新家", $"云端状态：在想象中运行良好\n本地历史：{state.History.Count} 条\n实际上上传：0 字节\n\n同步冲突：设备 A 的 2 与设备 B 的 2 完全相同。请不要担心，我们还是为此准备了一个对话框。", "保留这两个 2", "返回本地");
        Notify("冲突已解决", "两个 2 都安全地留在了本地。没有发出网络请求。"); Save();
    }
    private async void Pilot_Click(object sender, RoutedEventArgs e)
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
            _ => $"经本地剧本深度分析，当前结果为 {ResultText.Text}。建议下一步：使用这个数字。分析完毕，消耗 0 积分。"
        };
        ThinkingBar.Visibility = Visibility.Collapsed; busy = false; Save();
    }
    private void Recommendation_Click(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        Navigate("calc"); ExpressionBox.Text = "365 × 0";
        ExpressionBox.Focus(FocusState.Programmatic);
        Notify("推荐已应用", "多少虚构权益乘以零，都是免费的。");
    }
    private async void Optimize_Click(object sender, RoutedEventArgs e)
    {
        state.Ceremonies++;
        var rating = new RatingControl { Caption = "请评价本次等号", MaxRating = 5, Value = 5 };
        var panel = new StackPanel { Spacing = 16 };
        panel.Children.Add(new TextBlock { Text = "体验越多，分数越高。计算正确率不参与评分。", TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(rating);
        if (await Dialog("你的反馈对我们至关重要", panel, "本地提交反馈", "以后再说") == ContentDialogResult.Primary)
        { HealthText.Text = $"已收到 {rating.Value:0} 星。反馈留在本次会话中。"; Notify("感谢反馈", "我们将优先考虑让按钮更圆。"); }
        Save();
    }
    private async void Legacy_Click(object sender, RoutedEventArgs e)
    {
        state.Ceremonies++;
        var panel = new StackPanel { Spacing = 12, Background = new SolidColorBrush(ColorHelper.FromArgb(255, 236, 233, 216)), Padding = new Thickness(16) };
        panel.Children.Add(new TextBlock { Text = "数字属性   |   常规   |   高级", FontFamily = new FontFamily("Tahoma"), Foreground = new SolidColorBrush(Colors.Black) });
        panel.Children.Add(new TextBlock { Text = "此设备工作正常。\n\n数字驱动程序：decimal.sys（虚构）\n精度：约 29 位有效数字\n百分数：x% = x / 100\n平方根：使用浮点近似\n\n现代设置暂未迁移这个页面。", TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Colors.Black) });
        panel.Children.Add(new CheckBox { Content = "使用圆角兼容模式（仅供欣赏）", IsChecked = true, IsEnabled = false });
        await Dialog("高级数字属性（传统）", panel, "确定", "取消", "应用"); Save();
    }
    private async void Update_Click(object sender, RoutedEventArgs e)
    {
        if (busy || dialogOpen) return;
        state.Ceremonies++;
        var choice = await Dialog("体验更新已就绪", "SuperCalc 体验包 KB000042（模拟）\n\n• 等号圆角增加 0.5 px\n• 改进“改进体验”的体验\n• 将一个设置移动到另一个设置\n\n实际版本不变，无下载、无系统修改。", "模拟安装", "推迟到下一个等号");
        if (choice == ContentDialogResult.Primary)
        {
            busy = true;
            StatusText.Text = "正在优化圆角… 99%";
            if (state.Animations && Satire) await Task.Delay(750);
            if (isClosed) return;
            busy = false;
            Notify("体验更新完成", "没有任何文件被下载。您现在可以体验完全相同的计算器。");
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
        state.History.Clear(); Save(); RenderHistory();
    }

    private void BuildMuseum()
    {
        var exhibits = new[]
        {
            ("01 / 首次启动", "三页欢迎向导、接受推荐、账户归属感、完成设置、可跳过的仪式。"),
            ("02 / 到处都是入口", "Fluent 侧栏、PREVIEW 徽章、OneNumber 云盘、365 套餐、体验健康分。"),
            ("03 / 输入一个数字", "推荐数字、赞助式灵感、占据侧栏的 AI、答案语气、虚构积分。"),
            ("04 / 按下等号", "准备工作空间、应用圆角、兼容性检查、计算后的推荐与反馈提醒。"),
            ("05 / 复制一个结果", "分享优先、显示更多选项、传统剪贴板属性、跨两代界面才到复制。"),
            ("06 / 寻找历史", "本地搜索前的网页式推荐、计算时间线、再次体验按钮、最多 100 条本地历史。"),
            ("07 / 调整设置", "多级面包屑、把广告叫推荐、相关设置、复古控制面板、无用兼容开关。"),
            ("08 / 服务与更新", "免费虚构订阅、假云端冲突、KB000042 圆角更新、99% 等待。"),
            ("09 / 准备离开", "退出挽留、完成体验或直接退出、星级反馈、一键专注逃生口。"),
            ("10 / 设计不是敌人", "好看的界面与实用性可以兼得。讽刺的是把业务指标放在用户任务之前。所有模拟均在本地，不上传、不收费。")
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
    { if (isClosed) return; Notice.Title = title; Notice.Message = message; Notice.Severity = severity; Notice.IsOpen = true; }

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
        var choice = await Dialog("你的生产力旅程还未结束", $"你已完成 {state.Calculations} 次计算，体验了 {state.Ceremonies} 次额外流程。\n\n直接退出会错过很多我们刚刚想出来的可能性。", "直接退出", "继续计算", "再体验一下");
        if (choice == ContentDialogResult.Primary)
        {
            allowClose = true;
            CloseSafely();
        }
        else if (choice == ContentDialogResult.Secondary) Optimize_Click(this, new RoutedEventArgs());
    }
}
