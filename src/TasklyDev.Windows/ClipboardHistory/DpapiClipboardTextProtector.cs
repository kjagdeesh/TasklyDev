using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace TasklyDev.Windows.ClipboardHistory;

/// <summary>Uses Windows DPAPI to encrypt each clipboard entry for the current user.</summary>
public sealed class DpapiClipboardTextProtector : IClipboardTextProtector
{
    private const uint UiForbidden = 0x1;

    /// <inheritdoc />
    public string Protect(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var plainBytes = Encoding.UTF8.GetBytes(text);
        var inputPointer = Marshal.AllocHGlobal(plainBytes.Length);
        Marshal.Copy(plainBytes, 0, inputPointer, plainBytes.Length);
        var input = new DataBlob(plainBytes.Length, inputPointer);
        try
        {
            if (!CryptProtectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, UiForbidden, out var output))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not encrypt this clipboard entry.");
            }

            try
            {
                var protectedBytes = new byte[output.Size];
                Marshal.Copy(output.Data, protectedBytes, 0, output.Size);
                return Convert.ToBase64String(protectedBytes);
            }
            finally
            {
                LocalFree(output.Data);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plainBytes);
            Marshal.Copy(plainBytes, 0, inputPointer, plainBytes.Length);
            Marshal.FreeHGlobal(inputPointer);
        }
    }

    /// <inheritdoc />
    public string Unprotect(string protectedText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedText);
        byte[] protectedBytes;
        try
        {
            protectedBytes = Convert.FromBase64String(protectedText);
        }
        catch (FormatException exception)
        {
            throw new CryptographicException("Clipboard history contains an invalid encrypted value.", exception);
        }

        var inputPointer = Marshal.AllocHGlobal(protectedBytes.Length);
        Marshal.Copy(protectedBytes, 0, inputPointer, protectedBytes.Length);
        var input = new DataBlob(protectedBytes.Length, inputPointer);
        try
        {
            if (!CryptUnprotectData(ref input, out var description, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, UiForbidden, out var output))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not decrypt this clipboard history for the current user.");
            }

            try
            {
                var plainBytes = new byte[output.Size];
                Marshal.Copy(output.Data, plainBytes, 0, output.Size);
                try
                {
                    return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                        .GetString(plainBytes);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(plainBytes);
                }
            }
            finally
            {
                if (output.Data != IntPtr.Zero && output.Size > 0)
                {
                    Marshal.Copy(new byte[output.Size], 0, output.Data, output.Size);
                }

                if (description != IntPtr.Zero)
                {
                    LocalFree(description);
                }

                LocalFree(output.Data);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(inputPointer);
            CryptographicOperations.ZeroMemory(protectedBytes);
        }
    }

    [DllImport("Crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(
        ref DataBlob dataIn,
        string? dataDescription,
        IntPtr optionalEntropy,
        IntPtr reserved,
        IntPtr promptStructure,
        uint flags,
        out DataBlob dataOut);

    [DllImport("Crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(
        ref DataBlob dataIn,
        out IntPtr dataDescription,
        IntPtr optionalEntropy,
        IntPtr reserved,
        IntPtr promptStructure,
        uint flags,
        out DataBlob dataOut);

    [DllImport("Kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob(int size, IntPtr data)
    {
        public int Size = size;
        public IntPtr Data = data;
    }
}
