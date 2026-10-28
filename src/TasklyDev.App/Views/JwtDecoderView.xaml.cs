using System.Windows.Controls;

namespace TasklyDev.Views;

/// <summary>Displays local JWT decoding controls and explicitly untrusted token contents.</summary>
public partial class JwtDecoderView : UserControl
{
    /// <summary>Creates the JWT decoder view.</summary>
    public JwtDecoderView()
    {
        InitializeComponent();
    }
}
