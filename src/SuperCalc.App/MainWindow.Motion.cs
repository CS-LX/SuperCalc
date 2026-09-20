using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI.ViewManagement;

namespace SuperCalc.App;

public sealed partial class MainWindow
{
    private readonly UISettings uiSettings = new();
    private readonly List<Visual> animatedVisuals = [];
    private bool MotionEnabled => state.Animations && uiSettings.AnimationsEnabled;

    private void InitializeMotion()
    {
        foreach (var button in Keypad.Children.OfType<Button>())
        {
            button.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((_, _) => AnimateKey(button, true)), true);
            button.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler((_, _) => AnimateKey(button, false)), true);
            button.PointerCanceled += (_, _) => AnimateKey(button, false);
            button.PointerCaptureLost += (_, _) => AnimateKey(button, false);
            button.PointerExited += (_, _) => AnimateKey(button, false);
            button.Click += (_, _) => AnimateKey(button, false);
        }
    }

    private Visual MotionVisual(FrameworkElement element)
    {
        ElementCompositionPreview.SetIsTranslationEnabled(element, true);
        var visual = ElementCompositionPreview.GetElementVisual(element);
        if (!animatedVisuals.Contains(visual)) animatedVisuals.Add(visual);
        return visual;
    }

    private void AnimateKey(Button button, bool pressed)
    {
        if (!MotionEnabled || isClosed) return;
        var visual = MotionVisual(button);
        visual.CenterPoint = new Vector3((float)button.ActualWidth / 2, (float)button.ActualHeight / 2, 0);
        var animation = visual.Compositor.CreateVector3KeyFrameAnimation();
        animation.InsertKeyFrame(1, new Vector3(pressed ? .965f : 1));
        animation.Duration = TimeSpan.FromMilliseconds(pressed ? 70 : 140);
        visual.StartAnimation("Scale", animation);
    }

    private void AnimateEntrance(FrameworkElement element)
    {
        if (!MotionEnabled || isClosed || element.Visibility != Visibility.Visible) return;
        var visual = MotionVisual(element);
        var easing = visual.Compositor.CreateCubicBezierEasingFunction(new Vector2(.1f, .9f), new Vector2(.2f, 1));
        var slide = visual.Compositor.CreateVector3KeyFrameAnimation();
        slide.InsertKeyFrame(0, new Vector3(0, 10, 0));
        slide.InsertKeyFrame(1, Vector3.Zero, easing);
        slide.Duration = TimeSpan.FromMilliseconds(200);
        visual.StartAnimation("Translation", slide);
        var fade = visual.Compositor.CreateScalarKeyFrameAnimation();
        fade.InsertKeyFrame(0, .3f); fade.InsertKeyFrame(1, 1);
        fade.Duration = TimeSpan.FromMilliseconds(160);
        visual.StartAnimation("Opacity", fade);
    }

    private void AnimateError()
    {
        if (!MotionEnabled || isClosed) return;
        var visual = MotionVisual(ExpressionBox);
        var animation = visual.Compositor.CreateVector3KeyFrameAnimation();
        animation.InsertKeyFrame(0, Vector3.Zero);
        animation.InsertKeyFrame(.2f, new Vector3(-3, 0, 0));
        animation.InsertKeyFrame(.5f, new Vector3(3, 0, 0));
        animation.InsertKeyFrame(.8f, new Vector3(-1, 0, 0));
        animation.InsertKeyFrame(1, Vector3.Zero);
        animation.Duration = TimeSpan.FromMilliseconds(220);
        visual.StartAnimation("Translation", animation);
    }

    private void ResetMotion()
    {
        foreach (var visual in animatedVisuals)
        {
            visual.StopAnimation("Opacity"); visual.Opacity = 1;
            visual.StopAnimation("Scale"); visual.Scale = Vector3.One;
            visual.StopAnimation("Translation"); visual.Properties.InsertVector3("Translation", Vector3.Zero);
        }
    }

    private void RenderRecent()
    {
        RecentItems.Children.Clear();
        if (state.History.Count == 0)
            RecentItems.Children.Add(new TextBlock { Text = "尚无历史记录", Margin = new Thickness(8, 20, 8, 0), TextWrapping = TextWrapping.Wrap });
        foreach (var entry in state.History.Take(20))
        {
            var content = new StackPanel { Spacing = 5, HorizontalAlignment = HorizontalAlignment.Stretch };
            content.Children.Add(new TextBlock { Text = entry.Expression + " =", FontSize = 12, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Right });
            content.Children.Add(new TextBlock { Text = entry.Result, FontSize = 24, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Right });
            var button = new Button { Content = content, Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), BorderThickness = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(8, 12, 8, 12) };
            button.Click += (_, _) => { if (!busy) { ExpressionBox.Text = entry.Expression; ExpressionBox.Focus(FocusState.Programmatic); } };
            RecentItems.Children.Add(button);
        }
    }
}
