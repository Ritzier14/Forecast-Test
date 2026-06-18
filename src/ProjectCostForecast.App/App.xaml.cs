using System.Globalization;
using System.Windows;

namespace ProjectCostForecast.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        var nz = new CultureInfo("en-NZ");
        CultureInfo.DefaultThreadCurrentCulture = nz;
        CultureInfo.DefaultThreadCurrentUICulture = nz;
        base.OnStartup(e);
    }
}
