using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using SuperCalc.App.Controls;

namespace SuperCalc.App;

public sealed partial class MainWindow
{
    private ContentDialog? activeDialog;
    private bool suggestionNotice;

    private ContentDialog CreateExperienceDialog(string title, object content, string primary, string close = "关闭", string? secondary = null)
    {
        if (content is string text) content = ExperienceContent.FromText(title, text);
        if (content is FrameworkElement element) element.Width = Math.Max(220, Math.Min(520, Root.ActualWidth - 96));
        var dialog = new ContentDialog
        {
            XamlRoot = Root.XamlRoot, RequestedTheme = Root.ActualTheme, Title = title, Content = content,
            PrimaryButtonText = primary, CloseButtonText = close, SecondaryButtonText = secondary ?? "",
            DefaultButton = ContentDialogButton.Primary
        };
        dialog.Resources["ContentDialogMaxWidth"] = 640d;
        dialog.Opened += (_, _) => { if (content is FrameworkElement target) AnimateEntrance(target); };
        return dialog;
    }

    private async Task<ContentDialogResult> ShowExperienceDialog(ContentDialog dialog)
    {
        if (dialogOpen || isClosed) return ContentDialogResult.None;
        dialogOpen = true; activeDialog = dialog;
        SizeChangedEventHandler resize = (_, _) => { if (dialog.Content is FrameworkElement body) body.Width = Math.Max(220, Math.Min(520, Root.ActualWidth - 96)); };
        Root.SizeChanged += resize;
        try { return await dialog.ShowAsync(); }
        finally
        {
            Root.SizeChanged -= resize;
            if (dialog.Content is FrameworkElement content) animatedVisuals.Remove(ElementCompositionPreview.GetElementVisual(content));
            activeDialog = null; dialogOpen = false;
        }
    }

    private ContentDialog CreateSetupDialog(SetupWizard wizard)
    {
        var dialog = CreateExperienceDialog("让我们完成设备设置", wizard, "继续", "以后再说");
        void Refresh()
        {
            dialog.PrimaryButtonText = wizard.Step == 2 ? "接受并完成" : "继续";
            dialog.SecondaryButtonText = wizard.Step > 0 ? "上一步" : "";
            if (wizard.IsLoaded) AnimateEntrance(wizard.TransitionTarget);
        }
        dialog.PrimaryButtonClick += (_, args) =>
        {
            if (wizard.Step == 2) return;
            args.Cancel = true; wizard.SetStep(wizard.Step + 1); Refresh();
        };
        dialog.SecondaryButtonClick += (_, args) => { args.Cancel = true; wizard.SetStep(wizard.Step - 1); Refresh(); };
        Refresh();
        return dialog;
    }

    private async Task Onboard()
    {
        if (dialogOpen || busy) return;
        var wizard = new SetupWizard(state);
        state.Ceremonies++;
        var result = await ShowExperienceDialog(CreateSetupDialog(wizard));
        animatedVisuals.Remove(ElementCompositionPreview.GetElementVisual(wizard.TransitionTarget));
        if (isClosed) return;
        if (result == ContentDialogResult.Primary)
        {
            wizard.Apply(state);
            // Synchronize controls together; their handlers otherwise overwrite the remaining preferences.
            ready = false;
            DramaToggle.IsOn = state.DramaticMode; RecommendationsToggle.IsOn = state.Recommendations; AnimationToggle.IsOn = state.Animations;
            ready = true;
            if (!state.Animations) ResetMotion();
            Notify("你已准备就绪", "你的偏好已保存。现在可以继续计算。", InfoBarSeverity.Success);
        }
        state.Onboarded = true; ApplyMode(); Save();
    }

    private async Task<bool> PrepareCalculation(string expression)
    {
        if (!Drama || !MotionEnabled) return true;
        state.Ceremonies++;
        var preparation = new CalculationPreparation(expression);
        var dialog = CreateExperienceDialog("正在为你准备结果", preparation, "直接查看结果", "取消");
        var completed = false;
        var closing = false;
        dialog.Closing += (_, _) => closing = true;
        var pending = ShowExperienceDialog(dialog);
        for (var stage = 0; stage < 4 && !pending.IsCompleted && !closing; stage++)
        {
            preparation.SetStage(stage);
            await Task.WhenAny(pending, Task.Delay(stage == 3 ? 650 : 240));
            if (isClosed) return false;
            if (closing || !Drama || !MotionEnabled) break;
        }
        if (!pending.IsCompleted && !closing) { completed = true; dialog.Hide(); }
        var result = await pending;
        return completed || result == ContentDialogResult.Primary;
    }

    private (ExperienceContent Content, RadioButton Assistant, CheckBox Remember) CreateResultChoice()
    {
        var content = new ExperienceContent("选择适合你的结果体验", "此设置将用于后续计算。你也可以仅为这一次选择打开方式。", "\uE8A5", "打开结果的方式");
        var assistant = new RadioButton { GroupName = "ResultApp", IsChecked = state.OpenResultsWithAssistant, Content = new StackPanel { Spacing = 4, Children = { new TextBlock { Text = "CalcPilot", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold }, new TextBlock { Text = "推荐 · 解释结果并提供下一步建议", TextWrapping = TextWrapping.Wrap, FontSize = 12 } } } };
        var standard = new RadioButton { Name = "StandardResultOption", GroupName = "ResultApp", IsChecked = !state.OpenResultsWithAssistant, Content = "SuperCalc 标准计算器" };
        foreach (var option in new[] { assistant, standard }) content.Body.Children.Add(new Border { Style = (Style)Application.Current.Resources["Card"], Child = option });
        var remember = new CheckBox { Content = "始终使用此应用打开计算结果", IsChecked = true };
        content.Body.Children.Add(remember);
        content.SetFootnote("你可以在“设置 > 高级计算设置”中更改结果的打开方式。");
        return (content, assistant, remember);
    }

    private async void ChooseResult_Click(object sender, RoutedEventArgs e) => await ChooseResultExperience();
    private async Task ChooseResultExperience()
    {
        if (busy || dialogOpen) return;
        var choice = CreateResultChoice();
        if (await Dialog("你希望如何打开此结果？", choice.Content, "确定", "取消") != ContentDialogResult.Primary || isClosed) return;
        var useAssistant = choice.Assistant.IsChecked == true;
        if (choice.Remember.IsChecked == true) { state.OpenResultsWithAssistant = useAssistant; state.ResultExperienceChosen = true; Save(); }
        if (useAssistant)
        {
            if (Root.ActualWidth < 720) { await RunPilot(); if (!isClosed) await Dialog("CalcPilot", PilotReply.Text, "完成"); }
            else { assistantRequested = true; FocusToggle.IsOn = false; Navigate("calc"); SideTabs.SelectedIndex = 0; ApplyMode(); await RunPilot(); }
        }
        else { assistantRequested = false; Navigate("calc"); ApplyMode(); }
    }

    private void OfferNextStep()
    {
        if (!Drama || !state.Recommendations) return;
        if (!state.ResultExperienceChosen && state.Calculations % 3 == 0)
        {
            Notify("为结果选择默认应用", "使用 CalcPilot 获取解释和建议，充分利用每一次计算。");
            var action = new Button { Content = "选择应用" }; action.Click += ChooseResult_Click; Notice.ActionButton = action;
            suggestionNotice = true;
        }
        else if (state.Calculations % 5 == 0)
        {
            Notify("建议使用推荐设置", "再完成几个步骤，即可获取完整的 SuperCalc 体验。");
            var action = new Button { Content = "查看设置" }; action.Click += Onboarding_Click; Notice.ActionButton = action;
            suggestionNotice = true;
        }
        else if (state.Calculations % 4 == 0 && !state.PremiumPretend)
        {
            Notify("使用 SuperCalc 365 做到更多", "为你的结果添加更丰富的工作流。");
            var action = new Button { Content = "查看权益" }; action.Click += Premium_Click; Notice.ActionButton = action;
            suggestionNotice = true;
        }
    }
}
