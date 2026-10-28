using System.Windows;

namespace TasklyDev;

/// <summary>WPF application entry point and application-wide resource owner.</summary>
public partial class App : Application
{
    public App()
    {
        SessionEnding += App_SessionEnding;
    }

    private void App_SessionEnding(object sender, SessionEndingCancelEventArgs e)
    {
        if (MainWindow is TasklyDev.MainWindow mainWindow)
        {
            mainWindow.PrepareForApplicationExit();
        }
    }
}
