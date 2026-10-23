using System.Windows;
using System.Windows.Input;
using UmaParser.ViewModels;

namespace UmaParser.Views;

public partial class OptionsWindow : Window
{
    private readonly OptionsViewModel _viewModel;

    public OptionsWindow(OptionsViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
            return;
        }

        base.OnPreviewKeyDown(e);
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Save())
        {
            Close();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private void Apply_Click(object sender, RoutedEventArgs e) => _viewModel.Save();

    private void MoveUp_Click(object sender, RoutedEventArgs e) => _viewModel.MoveUp();

    private void MoveDown_Click(object sender, RoutedEventArgs e) => _viewModel.MoveDown();

    private void RestoreDefaults_Click(object sender, RoutedEventArgs e) => _viewModel.RestoreDefaults();
}
