using System.Windows.Controls;
using UmaParser.Themes;

namespace UmaParser.Views;

public partial class PerformanceView : UserControl
{
    public PerformanceView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            AppTheme.Changed -= OnThemeChanged;
            AppTheme.Changed += OnThemeChanged;
        };
        Unloaded += (_, _) => AppTheme.Changed -= OnThemeChanged;
    }

    private void OnThemeChanged(object? sender, EventArgs e) => ChartView.InvalidateVisual();
}
