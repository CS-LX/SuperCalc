using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
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
            Check("Focus mode removes distractions", Sidebar.Visibility == Visibility.Collapsed && AssistantPanel.Visibility == Visibility.Collapsed && RecommendationCard.Visibility == Visibility.Collapsed);
            Check("Focus mode disables ceremony", !Drama);
            await Capture(Path.Combine(outputDirectory, "04-focus.png"));
            FocusToggle.IsOn = false;
            Check("Full experience returns", WelcomeBanner.Visibility == Visibility.Visible);
            RecommendationsToggle.IsOn = false;
            Check("Recommendation setting actually hides cards", RecommendationCard.Visibility == Visibility.Collapsed && SearchPromotion.Visibility == Visibility.Collapsed);
            RecommendationsToggle.IsOn = true;
            Navigate("museum");
            Check("Design museum includes all lifecycle stages", MuseumItems.Children.Count == 10);
            await Capture(Path.Combine(outputDirectory, "05-museum.png"));
            Navigate("calc");
            ExpressionBox.Text = "(128 + 256) × 2"; await Calculate();
            // Test real ContentDialog layout and dismissal without interacting with the user's desktop.
            var probe = new ContentDialog
            {
                XamlRoot = Root.XamlRoot, Title = "让我们完成计算器的设置",
                Content = new TextBlock { Text = "1 / 3  欢迎来到数字生活的新篇章\n\n在您计算 2 + 2 之前，我们希望先了解您的梦想。\n\n所有账号、云、订阅与 AI 只在本地演出。", TextWrapping = TextWrapping.Wrap, MaxWidth = 440 },
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
            File.WriteAllText(Path.Combine(outputDirectory, "results.json"), JsonSerializer.Serialize(new { success = true, checks }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception e)
        {
            File.WriteAllText(Path.Combine(outputDirectory, "results.json"), JsonSerializer.Serialize(new { success = false, checks, error = e.ToString() }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { allowClose = true; CloseSafely(); }
    }

    private async Task Capture(string path, FrameworkElement? target = null)
    {
        Root.UpdateLayout();
        await Task.Delay(150);
        var bitmap = new RenderTargetBitmap();
        await bitmap.RenderAsync(target ?? Root);
        var pixels = await bitmap.GetPixelsAsync();
        var file = await StorageFile.GetFileFromPathAsync(CreateEmptyFile(path));
        using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, pixels.ToArray());
        await encoder.FlushAsync();
    }
    private static string CreateEmptyFile(string path) { File.WriteAllBytes(path, []); return Path.GetFullPath(path); }
}
