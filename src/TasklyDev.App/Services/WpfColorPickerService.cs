using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using TasklyDev.Core.Colors;

namespace TasklyDev.Services;

public sealed class WpfColorPickerService : IColorPickerService
{
    private const uint CcRgbInit = 0x00000001;
    private const uint CcFullOpen = 0x00000002;
    private const uint CcAnyColor = 0x00000100;
    private const int CustomColorCount = 16;

    public Task<ColorValue?> PickColorAsync(ColorValue initialColor)
    {
        var customColors = Marshal.AllocHGlobal(sizeof(uint) * CustomColorCount);
        try
        {
            for (var index = 0; index < CustomColorCount; index++)
            {
                Marshal.WriteInt32(customColors, index * sizeof(uint), 0);
            }

            var dialog = new ChooseColorData
            {
                Size = (uint)Marshal.SizeOf<ChooseColorData>(),
                Owner = GetOwnerHandle(),
                InitialColor = ToColorRef(initialColor),
                CustomColors = customColors,
                Flags = CcRgbInit | CcFullOpen | CcAnyColor
            };

            if (ChooseColor(ref dialog))
            {
                return Task.FromResult<ColorValue?>(
                    new ColorValue(
                        (byte)(dialog.InitialColor & 0xFF),
                        (byte)((dialog.InitialColor >> 8) & 0xFF),
                        (byte)((dialog.InitialColor >> 16) & 0xFF)));
            }

            var error = CommDlgExtendedError();
            if (error != 0)
            {
                throw new Win32Exception((int)error, "The Windows color selection dialog could not be opened.");
            }

            return Task.FromResult<ColorValue?>(null);
        }
        finally
        {
            Marshal.FreeHGlobal(customColors);
        }
    }

    private static IntPtr GetOwnerHandle()
    {
        var owner = Application.Current?.MainWindow;
        return owner is { IsVisible: true }
            ? new WindowInteropHelper(owner).Handle
            : IntPtr.Zero;
    }

    private static uint ToColorRef(ColorValue color) =>
        (uint)(color.Red | (color.Green << 8) | (color.Blue << 16));

    [DllImport("comdlg32.dll", EntryPoint = "ChooseColorW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ChooseColor(ref ChooseColorData chooseColor);

    [DllImport("comdlg32.dll", EntryPoint = "CommDlgExtendedError", SetLastError = true)]
    private static extern uint CommDlgExtendedError();

    [StructLayout(LayoutKind.Sequential)]
    private struct ChooseColorData
    {
        public uint Size;
        public IntPtr Owner;
        public IntPtr Instance;
        public uint InitialColor;
        public IntPtr CustomColors;
        public uint Flags;
        public IntPtr CustomData;
        public IntPtr Hook;
        public IntPtr TemplateName;
    }
}
