namespace TasklyDev.Core.Encoding;

/// <summary>Defines character encodings available to URL percent encoding and decoding.</summary>
public enum UrlCharacterEncoding
{
    /// <summary>Unicode encoded as UTF-8.</summary>
    Utf8 = 65001,

    /// <summary>Seven-bit US ASCII.</summary>
    Ascii = 20127,

    /// <summary>ISO-8859-1 Western European (Latin-1).</summary>
    Iso88591 = 28591,

    /// <summary>Windows Western European code page.</summary>
    Windows1252 = 1252,

    /// <summary>Little-endian UTF-16.</summary>
    Utf16LittleEndian = 1200
}
