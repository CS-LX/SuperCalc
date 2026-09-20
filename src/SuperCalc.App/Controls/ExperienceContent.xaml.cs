using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SuperCalc.App.Controls;

public sealed partial class ExperienceContent : UserControl
{
    public StackPanel Body => Items;
    public ExperienceContent() => InitializeComponent();
    public ExperienceContent(string heading, string description, string glyph = "\uE8EF", string eyebrow = "SUPERCALC") : this()
    { Heading.Text = heading; Description.Text = description; HeroIcon.Glyph = glyph; Eyebrow.Text = eyebrow; }
    public void SetFootnote(string text) { Footnote.Text = text; Footnote.Visibility = Visibility.Visible; }
    public static ExperienceContent FromText(string title, string text)
    {
        var sections = text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
        var glyph = title.Contains("OneNumber") ? "\uE753" : title.Contains("资料") ? "\uE77B" : title.Contains("更新") ? "\uE895" : title.Contains("分享") || title.Contains("剪贴板") ? "\uE72D" : "\uE946";
        var content = new ExperienceContent(title.Contains("OneNumber") ? "OneNumber" : title.Contains("CalcPilot") ? "CalcPilot" : "你的数字工作空间", sections.FirstOrDefault() ?? text, glyph);
        foreach (var section in sections.Skip(1))
            content.Body.Children.Add(new Border { Style = (Style)Application.Current.Resources["Card"], Child = new TextBlock { Text = section, TextWrapping = TextWrapping.Wrap, FontSize = 14, LineHeight = 22 } });
        return content;
    }
}
