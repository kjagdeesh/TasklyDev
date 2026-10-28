using System.Threading;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Media;
using TasklyDev;
using TasklyDev.ViewModels;
using TasklyDev.Views;
using Xunit;

namespace TasklyDev.Tests;

public sealed class ShellWindowTests
{
    [Fact]
    public void MainWindowLoadsItsResourcesAndTitle()
    {
        Exception? loadFailure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var app = new App();
                app.InitializeComponent();
                var window = new MainWindow();
                var windowViewModel = Assert.IsType<MainWindowViewModel>(window.DataContext);
                var base64View = new Base64View();
                var numberBaseView = new NumberBaseView();
                var unixTimestampView = new UnixTimestampView();
                var guidGeneratorView = new GuidGeneratorView();
                var hashCalculatorView = new HashCalculatorView();
                var colorConverterView = new ColorConverterView();
                var regexTesterView = new RegexTesterView();
                var processLookupView = new ProcessLookupView();
                var environmentVariableView = new EnvironmentVariableView();
                var settingsView = new SettingsView();

                Assert.Equal("taskly.dev", window.Title);
                Assert.Equal(WindowStyle.SingleBorderWindow, window.WindowStyle);
                Assert.Equal(ResizeMode.CanResize, window.ResizeMode);
                Assert.Equal(WindowState.Maximized, window.WindowState);
                Assert.True(window.ShowInTaskbar);
                Assert.Equal(1320, window.Width);
                Assert.NotNull(base64View);
                Assert.NotNull(numberBaseView);
                Assert.NotNull(unixTimestampView);
                Assert.NotNull(guidGeneratorView);
                Assert.NotNull(hashCalculatorView);
                Assert.NotNull(colorConverterView);
                Assert.NotNull(regexTesterView);
                Assert.NotNull(processLookupView);
                Assert.NotNull(environmentVariableView);
                Assert.NotNull(settingsView);

                window.Show();
                foreach (var route in new[]
                         {
                             "base64", "number-base", "timestamp", "guid", "hash",
                             "color", "regex", "process", "environment", "settings"
                         })
                {
                    windowViewModel.OpenToolCommand.Execute(route);
                    window.UpdateLayout();
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                }

                windowViewModel.SettingsViewModel.SelectedTheme = ThemePreference.Dark;
                Assert.Equal(Color.FromRgb(23, 25, 29), Assert.IsType<Color>(app.Resources["CanvasColor"]));
                Assert.Equal(Color.FromRgb(40, 44, 51), Assert.IsType<SolidColorBrush>(app.Resources["CardSurfaceBrush"]).Color);

                windowViewModel.SettingsViewModel.SelectedTheme = ThemePreference.Light;
                Assert.Equal(Color.FromRgb(239, 241, 246), Assert.IsType<Color>(app.Resources["CanvasColor"]));
                Assert.Equal(Colors.White, Assert.IsType<SolidColorBrush>(app.Resources["CardSurfaceBrush"]).Color);
                window.Close();
            }
            catch (Exception exception)
            {
                loadFailure = exception;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(loadFailure);
    }
}
