using System.Windows.Controls;
using System.Windows.Input;
using EpsonResetter.App.ViewModels;

namespace EpsonResetter.App.Views;

public partial class PrintersView : UserControl
{
    public PrintersView() => InitializeComponent();

    private void OnPrinterDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not PrintersViewModel viewModel || viewModel.SelectedPrinter is null)
        {
            return;
        }

        viewModel.OpenDetailsCommand.Execute(viewModel.SelectedPrinter);
    }
}