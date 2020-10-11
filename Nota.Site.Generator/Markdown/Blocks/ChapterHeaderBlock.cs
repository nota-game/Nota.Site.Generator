using AdaptMark.Parsers.Markdown;
using AdaptMark.Parsers.Markdown.Blocks;
using AdaptMark.Parsers.Markdown.Helpers;
using AdaptMark.Parsers.Markdown.Inlines;
using Nota.Site.Generator.Markdown.Blocks;
using Stasistium.Stages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Nota.Site.Generator.Markdown.Blocks
{

    internal class ChapterHeaderBlock : HeaderBlock
    {
        public string? ChapterId { get; set; }
        public string? HeroImage { get; set; }

        public new class Parser : Parser<ChapterHeaderBlock>
        {
            protected override void ConfigureDefaults(DefaultParserConfiguration configuration)
            {
                configuration.Before<HashParser>();
                base.ConfigureDefaults(configuration);
            }

            protected override BlockParseResult<ChapterHeaderBlock>? ParseInternal(in LineBlock markdown, int startLine, bool lineStartsNewParagraph, MarkdownDocument document)
            {
                var line = markdown[startLine];
                var firstNonSpace = line.IndexOfNonWhiteSpace();

                // This type of header starts with one or more '#' characters, followed by the header
                // text, optionally followed by any number of hash characters.
                if (firstNonSpace == -1 || firstNonSpace != 0 || line[firstNonSpace] != '#')
                {
                    return null;
                }

                var result = new ChapterHeaderBlock();

                // Figure out how many consecutive hash characters there are.
                int pos = 0;
                while (pos < line.Length && line[pos] == '#' && pos < 6)
                {
                    pos++;
                }

                result.HeaderLevel = pos;

                // space between hash an start of header text is ignored.
                while (pos < line.Length && line[pos] == ' ')
                {
                    pos++;
                }

                if (result.HeaderLevel == 0)
                {
                    return null;
                }

                var endOfHeader = line.Length;

                // Ignore any hashes at the end of the line.
                while (pos < endOfHeader && line[endOfHeader - 1] == '#')
                {
                    endOfHeader--;
                }

                // Ignore any space at the end of the line.
                while (pos < endOfHeader && line[endOfHeader - 1] == ' ')
                {
                    endOfHeader--;
                }

                // Parse the inline content.
                result.Inlines = document.ParseInlineChildren(line.Slice(pos, endOfHeader - pos), true, true);

                var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                      .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
                      .Build();

                var yamleBlock = markdown
                    .SliceLines(1)
                    .RemoveFromLine((l, i) =>
                    {
                        if (l == "---")
                            return (0, 0, true, true);
                        return (0, l.Length, false, false);
                    });

                if (yamleBlock.TextLength == 0)
                    return null;

                var deserelized = deserializer.Deserialize<ChapterYaml>(yamleBlock.ToString());

                if (deserelized is null)
                    return null;

                result.ChapterId = deserelized.ChapterId;
                result.HeroImage = deserelized.HeroImage;

                return BlockParseResult.Create(result, startLine, yamleBlock.LineCount + 1);
            }

            private class ChapterYaml
            {
                public string? ChapterId { get; set; }
                public string? HeroImage { get; set; }
            }
        }
    }
}
