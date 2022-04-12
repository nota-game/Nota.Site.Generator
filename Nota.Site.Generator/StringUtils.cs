
using System.IO;
using System.Text;

namespace Nota.Site.Generator;

internal static class StringUtils
{

    public static string ReadString(this Stream stream)
    {
        using (stream) {
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return Encoding.UTF8.GetString(buffer.ToArray());
        }
    }
    public static Stream ToStream(this string str)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(str));
    }

}
