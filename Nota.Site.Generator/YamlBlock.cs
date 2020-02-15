using Microsoft.Toolkit.Parsers.Markdown;
using Microsoft.Toolkit.Parsers.Markdown.Blocks;
using Microsoft.Toolkit.Parsers.Markdown.Helpers;
using System;

namespace Nota.Site.Generator.Markdown.Blocks
{
    public class YamlBlock<T> : MarkdownBlock
    {
        public YamlBlock(T data)
        {
            this.Data = data;
        }

        public T Data { get; }

        public new class Parser : Parser<YamlBlock<T>>
        {
            protected override BlockParseResult<YamlBlock<T>>? ParseInternal(string markdown, int startOfLine, int firstNonSpace, int endOfFirstLine, int maxStart, int maxEnd, bool lineStartsNewParagraph, MarkdownDocument document)
            {
                // This type of header starts with one or more '#' characters, followed by the header
                // text, optionally followed by any number of hash characters.
                if (firstNonSpace + 3 >= maxEnd || markdown[firstNonSpace] != '-' || markdown[firstNonSpace + 1] != '-' || markdown[firstNonSpace + 2] != '-' || firstNonSpace != startOfLine)
                {
                    return null;
                }
                var end = markdown.AsSpan(firstNonSpace).Slice(3).IndexOf("---", StringComparison.InvariantCulture);
                if (end < 0 || end + firstNonSpace + 3 + 3 > maxEnd)
                    return null;
                end += firstNonSpace + 3 /*removed through span above*/;

                var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                    .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
                    .Build();
                try
                {
                    var obj = deserializer.Deserialize<T>(markdown.Substring(firstNonSpace, end - firstNonSpace));
                    if (obj is null)
                        return null;
                    return BlockParseResult.Create(new YamlBlock<T>(obj), firstNonSpace, end + 3 /*for the last 3 dashes*/);
                }
                catch (YamlDotNet.Core.YamlException e)
                {
                    return null;
                }
            }
        }

        public override string ToString()
        {
            var serelizer = new YamlDotNet.Serialization.SerializerBuilder()
                .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
                .Build();

            return serelizer.Serialize(Data);
        }
    }
}
