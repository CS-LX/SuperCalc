using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Hosting;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace SuperCalc.App;

// An opt-in app-level integration harness. It uses a separate state directory and no external UI injection.
public sealed partial class MainWindow
{
    private async Task RunSmokeTest(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        var checks = new List<string>();
        void Check(string name, bool condition)
        {
            if (!condition) throw new InvalidOperationException("FAIL: " + name);
            checks.Add("PASS: " + name);
        }
        try
        {
            state.Onboarded = true;
            await Task.Delay(500);
            Check("Native XAML window loaded", Root.ActualWidth > 0 && Keypad.ActualHeight > 0);
            Check("Native navigation uses compact pane", Navigation.PaneDisplayMode == NavigationViewPaneDisplayMode.LeftCompact && !Navigation.IsPaneOpen);
            Check("Mica backdrop is installed", SystemBackdrop is MicaBackdrop);
            Check("Keypad is inside viewport without page scrolling", EqualsButton.TransformToVisual(Root).TransformPoint(new Point(0, EqualsButton.ActualHeight)).Y <= Root.ActualHeight - 24);
            ExpressionBox.Text = "(128 + 256) × 2";
            await Calculate();
            Check("Equals handler displays correct result", ResultText.Text == "768");
            Check("Calculation recorded in timeline", state.History[0].Result == "768");
            await Capture(Path.Combine(outputDirectory, "01-calculator.png"));
            Insert("+"); Insert("32"); await Calculate();
            Check("Keypad continues from previous answer", ResultText.Text == "800");
            Insert("9");
            Check("Digit starts a fresh calculation", ExpressionBox.Text == "9");
            Unary_Click(new Button { Tag = "square" }, new RoutedEventArgs());
            await Calculate();
            Check("Square action", ResultText.Text == "81");
            Memory_Click(new Button { Tag = "clear" }, new RoutedEventArgs());
            Memory_Click(new Button { Tag = "add" }, new RoutedEventArgs());
            Clear();
            Memory_Click(new Button { Tag = "recall" }, new RoutedEventArgs());
            Check("Memory recall", ExpressionBox.Text == "81");
            await Calculate();
            ExpressionBox.Text = "1/0";
            await Calculate();
            Check("Division-by-zero error is visible", ResultText.Text == "—" && Notice.IsOpen && Notice.Severity == InfoBarSeverity.Error);
            Clear();
            ExpressionBox.Text = "0.1+0.2";
            await Calculate();
            Check("Recovery after error and decimal correctness", ResultText.Text == "0.3");
            Navigate("history");
            HistorySearch.Text = "128"; RenderHistory();
            Check("Timeline search filters real entries", HistoryItems.Children.Count == 1);
            await Capture(Path.Combine(outputDirectory, "02-history.png"));
            Navigate("settings");
            await Capture(Path.Combine(outputDirectory, "03-settings.png"));
            FocusToggle.IsOn = true;
            Check("Focus mode returns to calculator", CalculatorPage.Visibility == Visibility.Visible);
            Check("Focus mode removes distractions", !Navigation.IsPaneVisible && AssistantPanel.Visibility == Visibility.Collapsed && RecommendationCard.Visibility == Visibility.Collapsed);
            Check("Focus mode disables ceremony", !Drama);
            await Capture(Path.Combine(outputDirectory, "04-focus.png"));
            FocusToggle.IsOn = false;
            Check("Full experience returns", WelcomeBanner.Visibility == Visibility.Visible);
            RecommendationsToggle.IsOn = false;
            Check("Recommendation setting actually hides cards", RecommendationCard.Visibility == Visibility.Collapsed && SearchPromotion.Visibility == Visibility.Collapsed);
            RecommendationsToggle.IsOn = true;
            Navigate("museum");
            Check("What's new contains feature guidance", MuseumItems.Children.Count == 10);
            await Capture(Path.Combine(outputDirectory, "05-museum.png"));
            Navigate("calc");
            ExpressionBox.Text = "(128 + 256) × 2"; await Calculate();
            // Test real ContentDialog layout and dismissal without interacting with the user's desktop.
            var probe = new ContentDialog
            {
                XamlRoot = Root.XamlRoot, Title = "让我们完成计算器的设置",
                Content = new TextBlock { Text = "1 / 3  充分利用 SuperCalc\n\n只需再完成几个步骤，即可让你的计算体验更加个性化。\n\n你的工作空间已准备就绪。让我们继续设置适合你的服务。", TextWrapping = TextWrapping.Wrap, MaxWidth = 440 },
                PrimaryButtonText = "接受并继续", CloseButtonText = "跳过，直接计算"
            };
            var pending = probe.ShowAsync();
            await Task.Delay(250);
            Check("ContentDialog opens", probe.IsLoaded);
            await Capture(Path.Combine(outputDirectory, "06-onboarding.png"), probe);
            probe.Hide(); await pending;
            Save();
            var reloaded = store.Load();
            Check("UI preferences and history persist", reloaded.Onboarded && reloaded.History.Count > 0);
            await Capture(Path.Combine(outputDirectory, "07-final.png"));
            AnimateKey(EqualsButton, true);
            AnimateKey(EqualsButton, false);
            AnimationToggle.IsOn = false;
            Check("Disabling motion resets key scale", ElementCompositionPreview.GetElementVisual(EqualsButton).Scale == System.Numerics.Vector3.One);
            Check("Motion preference disables custom animations", !MotionEnabled);
            AnimationToggle.IsOn = true;
            var originalSize = AppWindow.Size;
            var scale = Root.XamlRoot.RasterizationScale;
            AppWindow.Resize(new SizeInt32((int)(560 * scale), (int)(610 * scale)));
            await Task.Delay(300);
            Check("Narrow window hides secondary pane", AssistantPanel.Visibility == Visibility.Collapsed);
            Check("Narrow window preserves keypad access", EqualsButton.TransformToVisual(Root).TransformPoint(new Point(0, EqualsButton.ActualHeight)).Y <= Root.ActualHeight - 24);
            await Capture(Path.Combine(outputDirectory, "08-compact.png"));
            Navigate("settings");
            await Capture(Path.Combine(outputDirectory, "11-compact-settings.png"));
            Navigate("calc");
            AppWindow.Resize(originalSize);
            Root.RequestedTheme = ElementTheme.Dark;
            await Task.Delay(300);
            await Capture(Path.Combine(outputDirectory, "09-dark.png"));
            Check("Dark theme applies", Root.ActualTheme == ElementTheme.Dark);
            await CapturePremiumOffer(Path.Combine(outputDirectory, "12-premium-dark.png"));
            Root.RequestedTheme = ElementTheme.Light;
            await Task.Delay(250);
            await CapturePremiumOffer(Path.Combine(outputDirectory, "13-premium-light.png"));
            SideTabs.SelectedIndex = 1;
            Check("History is available beside the keypad", RecentItems.Children.Count > 0);
            await Capture(Path.Combine(outputDirectory, "10-side-history.png"));
            File.WriteAllText(Path.Combine(outputDirectory, "results.json"), JsonSerializer.Serialize(new { success = true, checks }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception e)
        {
            File.WriteAllText(Path.Combine(outputDirectory, "results.json"), JsonSerializer.Serialize(new { success = false, checks, error = e.ToString() }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { allowClose = true; CloseSafely(); }
    }

    private async Task CapturePremiumOffer(string path)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = Root.XamlRoot, RequestedTheme = Root.ActualTheme,
            Title = "使用 SuperCalc 365 做到更多", Content = new Controls.PremiumOffer(),
            PrimaryButtonText = "开始使用", CloseButtonText = "暂时跳过",
            DefaultButton = ContentDialogButton.Primary
        };
        var pending = dialog.ShowAsync();
        await Task.Delay(250);
        await Capture(path, dialog);
        dialog.Hide(); await pending;
    }

    private async Task Capture(string path, FrameworkElement? target = null)
    {
        Root.UpdateLayout();
        await Task.Delay(250);
        var bitmap = new RenderTargetBitmap();
        // Mica lives outside the XAML tree. Use its solid fallback while capturing XAML, then restore.
        var background = Root.Background;
        try
        {
            Root.Background = new SolidColorBrush(Root.ActualTheme == ElementTheme.Dark ? Microsoft.UI.ColorHelper.FromArgb(255, 32, 32, 32) : Microsoft.UI.ColorHelper.FromArgb(255, 243, 243, 243));
            await bitmap.RenderAsync(target ?? Root);
        }
        finally { Root.Background = background; }
        var pixels = await bitmap.GetPixelsAsync();
        var file = await StorageFile.GetFileFromPathAsync(CreateEmptyFile(path));
        using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, pixels.ToArray());
        await encoder.FlushAsync();
    }
    private static string CreateEmptyFile(string path) { File.WriteAllBytes(path, []); return Path.GetFullPath(path); }
}
