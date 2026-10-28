using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace TasklyDev.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private void SettingsBrandLogo_Loaded(object sender, RoutedEventArgs e)
    {
        var logoPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "TasklyDevLogo.png");
        if (!System.IO.File.Exists(logoPath))
        {
            return;
        }

        var logo = new BitmapImage();
        logo.BeginInit();
        logo.CacheOption = BitmapCacheOption.OnLoad;
        logo.UriSource = new Uri(logoPath, UriKind.Absolute);
        logo.EndInit();
        SettingsBrandLogo.Source = logo;
    }
}
