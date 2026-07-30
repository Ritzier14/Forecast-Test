using System.Globalization;
using System.Windows;
using ProjectCostForecast.App.ViewModels;

namespace ProjectCostForecast.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        var nz = new CultureInfo("en-NZ");
        CultureInfo.DefaultThreadCurrentCulture = nz;
        CultureInfo.DefaultThreadCurrentUICulture = nz;
        base.OnStartup(e);

        var dependencies = new MainWindowViewModelDependencies();
        var window = new MainWindow(new MainWindowViewModel(dependencies));
        MainWindow = window;
        window.Show();
    }
}
