using Microsoft.UI.Xaml.Controls;

namespace SuperCalc.App.Controls;

public sealed partial class CalculationPreparation : UserControl
{
    public int Stage { get; private set; }
    public CalculationPreparation() => InitializeComponent();
    public CalculationPreparation(string expression) : this() { Expression.Text = "当前算式：" + expression; }
    public void SetStage(int stage)
    {
        Stage = Math.Clamp(stage, 0, 3);
        var titles = new[] { "正在准备你的工作空间", "正在应用体验设置", "正在整理你的结果", "即将完成" };
        var values = new[] { 8, 42, 78, 99 };
        StageTitle.Text = titles[Stage]; Progress.Value = values[Stage]; Percent.Text = values[Stage] + "%";
        DeviceStep.Text = (Stage >= 1 ? "✓" : "○") + "  检查设备兼容性";
        SettingsStep.Text = (Stage >= 2 ? "✓" : "○") + "  应用体验设置";
        WorkspaceStep.Text = (Stage >= 3 ? "✓" : "○") + "  整理结果工作空间";
    }
}
