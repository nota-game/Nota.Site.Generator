using AdaptMark.Parsers.Markdown;
using AdaptMark.Parsers.Markdown.Blocks;
using AdaptMark.Parsers.Markdown.Helpers;
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
            protected override BlockParseResult<YamlBlock<T>>? ParseInternal(in LineBlock markdown, int startLine, bool lineStartsNewParagraph, MarkdownDocument document)
            {

                var firstLine = markdown[startLine];

                if (firstLine.Length < 3 || firstLine[0] != '-' || firstLine[1] != '-' || firstLine[2] != '-' || !firstLine.Slice(3).IsWhiteSpace())
                    return null;
                var content = markdown.SliceLines(startLine + 1);
                content = content.RemoveFromLine((l, i) =>
                 {
                     if (l.Length < 3 || l[0] != '-' || l[1] != '-' || l[2] != '-' || !l.Slice(3).IsWhiteSpace())
                         return (0, l.Length, false, false);

                     return (0, 0, true, true);
                 });

                int numberOfSeperatorLines;
                if (content.LineCount + startLine + 1 < markdown.LineCount)
                    numberOfSeperatorLines = 2;
                else
                    numberOfSeperatorLines = 1;

                var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                    .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
                    .Build();
                try
                {
                    var obj = deserializer.Deserialize<T>(content.ToString());
                    if (obj is null)
                        return null;
                    return BlockParseResult.Create(new YamlBlock<T>(obj), startLine, content.LineCount + numberOfSeperatorLines);
                }
                catch (YamlDotNet.Core.YamlException e)
                {
                    return null;
                }
            }
        }

        protected override string StringRepresentation()
        {
            var serelizer = new YamlDotNet.Serialization.SerializerBuilder()
                .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
                .Build();

            return serelizer.Serialize(Data);
        }
    }
}
