using System.Threading;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Media;
using System.Text;
using TasklyDev;
using TasklyDev.Core.ClipboardHistory;
using TasklyDev.Services;
using TasklyDev.ViewModels;
using TasklyDev.Views;
using TasklyDev.Windows.ClipboardHistory;
using Xunit;

namespace TasklyDev.Tests;

public sealed class ShellWindowTests
{
    [Fact]
    public void MainWindowLoadsItsResourcesAndTitle()
    {
        Exception? loadFailure = null;
        var historyDirectory = Path.Combine(Path.GetTempPath(), $"TasklyClipboardShellTest-{Guid.NewGuid():N}");
        var thread = new Thread(() =>
        {
            try
            {
                var app = new App();
                app.InitializeComponent();
                var testViewModel = new MainWindowViewModel(
                    clipboardHistoryService: new ClipboardHistoryService(
                        Path.Combine(historyDirectory, "clipboard-history.json"),
                        new TestTextProtector()),
                    clipboardHistoryConfirmation: new TestClipboardHistoryConfirmationService());
                var window = new MainWindow(testViewModel);
                var windowViewModel = Assert.IsType<MainWindowViewModel>(window.DataContext);
                var base64View = new Base64View();
                var numberBaseView = new NumberBaseView();
                var unixTimestampView = new UnixTimestampView();
                var guidGeneratorView = new GuidGeneratorView();
                var guidParserView = new GuidParserView();
                var urlCodecView = new UrlCodecView();
                var jwtDecoderView = new JwtDecoderView();
                var hashCalculatorView = new HashCalculatorView();
                var colorConverterView = new ColorConverterView();
                var regexTesterView = new RegexTesterView();
                var processLookupView = new ProcessLookupView();
                var environmentVariableView = new EnvironmentVariableView();
                var dotNetInspectorView = new DotNetInspectorView();
                var systemInformationView = new SystemInformationView();
                var clipboardHistoryView = new ClipboardHistoryView();
                var certificateInspectorView = new CertificateInspectorView();
                var settingsView = new SettingsView();
                var clearClipboardHistoryDialog = new ClearClipboardHistoryDialog();

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
                Assert.NotNull(guidParserView);
                Assert.NotNull(urlCodecView);
                Assert.NotNull(jwtDecoderView);
                Assert.NotNull(hashCalculatorView);
                Assert.NotNull(colorConverterView);
                Assert.NotNull(regexTesterView);
                Assert.NotNull(processLookupView);
                Assert.NotNull(environmentVariableView);
                Assert.NotNull(dotNetInspectorView);
                Assert.NotNull(systemInformationView);
                Assert.NotNull(clipboardHistoryView);
                Assert.NotNull(certificateInspectorView);
                Assert.NotNull(settingsView);
                Assert.NotNull(clearClipboardHistoryDialog);

                window.Show();
                foreach (var route in new[]
                         {
                             "base64", "number-base", "timestamp", "guid", "uuid-parser", "url-codec", "jwt-decoder", "hash",
                             "color", "regex", "process", "environment", "clipboard-history", "certificate-inspector", "settings"
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

        if (Directory.Exists(historyDirectory))
        {
            Directory.Delete(historyDirectory, recursive: true);
        }

        Assert.Null(loadFailure);
    }

    private sealed class TestTextProtector : IClipboardTextProtector
    {
        public string Protect(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));

        public string Unprotect(string protectedText) =>
            Encoding.UTF8.GetString(Convert.FromBase64String(protectedText));
    }

    private sealed class TestClipboardHistoryConfirmationService : IClipboardHistoryConfirmationService
    {
        public bool ConfirmClearHistory() => true;
    }
}
