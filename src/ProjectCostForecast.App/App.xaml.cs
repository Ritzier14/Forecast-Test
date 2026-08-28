using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using ProjectCostForecast.App.Services;
using ProjectCostForecast.App.ViewModels;

namespace ProjectCostForecast.App;

public partial class App : Application
{
    private readonly RuntimeExceptionPolicy _runtimeExceptionPolicy =
        new(new DiagnosticsService());

    protected override void OnStartup(StartupEventArgs e)
    {
        AttachRuntimeExceptionHandlers();

        try
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
        catch (Exception exception)
        {
            HandleUiFailure(exception);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DetachRuntimeExceptionHandlers();
        base.OnExit(e);
    }

    private void AttachRuntimeExceptionHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void DetachRuntimeExceptionHandlers()
    {
        DispatcherUnhandledException -= OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException -= OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
    }

    private void OnDispatcherUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        HandleUiFailure(e.Exception);
    }

    private void OnAppDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception
            ?? new InvalidOperationException("The application domain raised a non-exception failure.");
        _ = _runtimeExceptionPolicy.Handle(RuntimeExceptionBoundary.ApplicationDomain, exception);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _ = _runtimeExceptionPolicy.Handle(RuntimeExceptionBoundary.UnobservedTask, e.Exception);
        e.SetObserved();
    }

    private void HandleUiFailure(Exception exception)
    {
        var result = _runtimeExceptionPolicy.Handle(RuntimeExceptionBoundary.UiDispatcher, exception);

        try
        {
            MessageBox.Show(
                result.UserMessage,
                "Project Cost Forecast",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch
        {
            // The process still follows the fail-fast policy if the dialog
            // cannot be created during shutdown or application startup.
        }

        if (result.FailFast)
        {
            Shutdown(-1);
        }
    }
}
