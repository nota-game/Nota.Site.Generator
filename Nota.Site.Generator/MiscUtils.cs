
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using AdaptMark.Markdown.Blocks;
using AdaptMark.Parsers.Markdown.Blocks;
using AdaptMark.Parsers.Markdown.Inlines;

namespace Nota.Site.Generator;

internal static class MiscUtils
{

    public static IEnumerable<MarkdownBlock> GetBlocksRecursive(this IBlockContainer container)
    {
        foreach (var b in container.Blocks) {
            yield return b;
            if (b is IBlockContainer subContainer)
                foreach (var subB in GetBlocksRecursive(subContainer))
                    yield return b;
        }
    }
    public static IEnumerable<MarkdownInline> GetInlineRecursive(this IInlineContainer container)
    {
        foreach (var b in container.Inlines) {
            yield return b;
            if (b is IInlineContainer subContainer)
                foreach (var subB in GetInlineRecursive(subContainer))
                    yield return b;
        }
    }

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
