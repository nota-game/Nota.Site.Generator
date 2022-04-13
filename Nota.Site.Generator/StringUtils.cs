
using System.IO;
using System.Text;
using System.Threading.Tasks;

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
    public static async Task<byte[]> ReadBytes(this Stream stream)
    {
        using (stream) {
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            return buffer.ToArray();
        }
    }
    public static Stream ToStream(this string str)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(str));
    }

}
