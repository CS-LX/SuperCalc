using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SuperCalc.Core;

namespace SuperCalc.App.Controls;

public sealed partial class SetupWizard : UserControl
{
    public int Step { get; private set; }
    public FrameworkElement TransitionTarget => PageSurface;
    public SetupWizard() => InitializeComponent();
    public SetupWizard(AppState state) : this()
    { RecommendedOffers.IsChecked = state.Recommendations; EnhancedWorkflow.IsChecked = state.DramaticMode; MotionPreference.IsChecked = state.Animations; }
    public void SetStep(int step)
    {
        Step = Math.Clamp(step, 0, 2);
        var pages = new[] { WelcomePage, ProfilePage, PreferencesPage };
        var bars = new[] { StepBar0, StepBar1, StepBar2 };
        var labels = new[] { StepLabel0, StepLabel1, StepLabel2 };
        for (var i = 0; i < pages.Length; i++)
        { pages[i].Visibility = i == Step ? Visibility.Visible : Visibility.Collapsed; bars[i].Value = i <= Step ? 100 : 0; labels[i].Opacity = i <= Step ? 1 : .55; }
    }
    public void Apply(AppState state)
    {
        state.LocalPersona = PersonalProfile.IsChecked == true;
        state.Recommendations = RecommendedOffers.IsChecked == true;
        state.DramaticMode = EnhancedWorkflow.IsChecked == true;
        state.Animations = MotionPreference.IsChecked == true;
    }
}
