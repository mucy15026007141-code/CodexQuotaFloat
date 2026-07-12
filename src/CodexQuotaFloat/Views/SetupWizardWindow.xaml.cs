using System.Windows;
using CodexQuotaFloat.ViewModels;

namespace CodexQuotaFloat.Views;

public partial class SetupWizardWindow : Window
{
    public SetupWizardWindow(SetupWizardViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.SetupSucceeded += () => Dispatcher.Invoke(Close);
    }
}
