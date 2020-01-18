using Microsoft.Toolkit.Parsers.Markdown;
using Microsoft.Toolkit.Parsers.Markdown.Blocks;
using Microsoft.Toolkit.Parsers.Markdown.Helpers;
using Nota.Site.Generator.Markdown.Blocks;
using Stasistium.Stages;
using System;
using System.Collections.Generic;
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

            protected override BlockParseResult<ChapterHeaderBlock>? ParseInternal(string markdown, int startOfLine, int firstNonSpace, int endOfFirstLine, int maxStart, int maxEnd, bool lineStartsNewParagraph, MarkdownDocument document)
            {
                // This type of header starts with one or more '#' characters, followed by the header
                // text, optionally followed by any number of hash characters.
                if (markdown[firstNonSpace] != '#' || firstNonSpace != startOfLine)
                {
                    return null;
                }

                var result = new ChapterHeaderBlock();

                // Figure out how many consecutive hash characters there are.
                int pos = startOfLine;
                while (pos < endOfFirstLine && markdown[pos] == '#' && pos - startOfLine < 6)
                {
                    pos++;
                }

                result.HeaderLevel = pos - startOfLine;
                if (result.HeaderLevel == 0)
                {
                    return null;
                }

                var endOfHeader = endOfFirstLine;

                // Ignore any hashes at the end of the line.
                while (pos < endOfHeader && markdown[endOfHeader - 1] == '#')
                {
                    endOfHeader--;
                }

                // Parse the inline content.
                result.Inlines = document.ParseInlineChildren(markdown, pos, endOfHeader, Array.Empty<Type>());

                var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                         .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
                         .Build();

                var endSymbol = markdown.IndexOf("---", endOfHeader, maxEnd - endOfHeader);

                if (endSymbol == -1)
                    return null;

                endSymbol += 3;

                var deserelized = deserializer.Deserialize<ChapterYaml>(markdown.Substring(endOfHeader, endSymbol - endOfHeader));

                if (deserelized is null)
                    return null;

                result.ChapterId = deserelized.ChapterId;
                result.HeroImage = deserelized.HeroImage;

                return BlockParseResult.Create(result, startOfLine, endSymbol);

            }

            private class ChapterYaml
            {
                public string? ChapterId { get; set; }
                public string? HeroImage { get; set; }
            }
        }
    }
}
