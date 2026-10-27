namespace TasklyDev.Core.Encoding;

public interface IBase64Service
{
    string EncodeUtf8(string text, Base64Variant variant, bool includePadding);
    string DecodeUtf8(string encoded, Base64Variant variant);
}
