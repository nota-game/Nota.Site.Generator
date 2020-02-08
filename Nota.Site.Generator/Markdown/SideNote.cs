using Microsoft.Toolkit.Parsers.Markdown;
using Microsoft.Toolkit.Parsers.Markdown.Blocks;
using Microsoft.Toolkit.Parsers.Markdown.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Nota.Site.Generator.Markdown.Blocks
{
    public class SideNote : MarkdownBlock
    {
        public string Reference { get; set; }
        public SideNoteType SideNoteType { get; set; }

        public ImmutableArray<(string id, byte distribution)> Distributions { get; set; } = ImmutableArray<(string id, byte distribution)>.Empty;
        public IList<MarkdownBlock> Blocks { get; set; }

        public new class Parser : Parser<SideNote>
        {

            private readonly Regex distributionPatter = new Regex(@"(?<id>\S+)\s+(?<distribution>\d)", RegexOptions.Compiled);

            protected override BlockParseResult<SideNote>? ParseInternal(string markdown, int startOfLine, int firstNonSpace, int endOfFirstLine, int maxStart, int maxEnd, bool lineStartsNewParagraph, MarkdownDocument document)
            {
                if (markdown[startOfLine] != '|')
                    return null;

                var lines = new LineSplitter(markdown.AsSpan(startOfLine, maxEnd - startOfLine));
                bool blockTypeWasParsed = false;
                //var distributionList = ImmutableArray<>
                var result = new SideNote();
                var builder = result.Distributions.ToBuilder();


                var buffer = new StringBuilder();
                bool header = true;
                int lastend = 0;
                while (lines.TryGetNextLine(out var line, out var start, out var end))
                {
                    lastend = end;
                    if (line.Length == 0 || line[0] != '|')
                        break;
                    int textStart = 1;
                    if (line.Length >= 2 && line[1] == ' ')
                        textStart = 2;
                    var text = line[textStart..];

                    if (line.Length >= 4 && line[1] == '-' && line[2] == '-' && line[3] == '-')
                    {
                        header = false;
                        continue; // we do not use this line anymore
                    }

                    if (header)
                    {
                        if (result.Reference == null)
                        {
                            result.Reference = text.ToString();
                        }
                        else if (!blockTypeWasParsed)
                        {
                            blockTypeWasParsed = true;
                            if (!Enum.TryParse<SideNoteType>(text.ToString(), out var parsed))
                                parsed = SideNoteType.Undefined;
                            result.SideNoteType = parsed;
                        }
                        else
                        {
                            var matches = this.distributionPatter.Matches(text.ToString());

                            builder.AddRange(matches.Select(x => (x.Groups["id"].Value, byte.Parse(x.Groups["distribution"].Value))));
                        }
                    }
                    else
                    {
                        buffer.Append(text);
                        buffer.AppendLine();
                    }
                }

                /**/
                result.Distributions = builder.ToImmutable();

                var blocks = document.ParseBlocks(buffer.ToString(), 0, buffer.Length, out _);

                result.Blocks = blocks;
                return BlockParseResult.Create(result, startOfLine, lastend + startOfLine);
            }


        }
    }
    public ref struct LineSplitter
    {
        private readonly ReadOnlySpan<char> text;

        private int currentIndex;

        public LineSplitter(ReadOnlySpan<char> readOnlySpan) : this()
        {
            this.text = readOnlySpan;
        }

        public bool TryGetNextLine(out ReadOnlySpan<char> line, out int lineStart, out int lineEnd)
        {
            if (this.currentIndex >= this.text.Length)
            {
                line = ReadOnlySpan<char>.Empty;
                lineStart = -1;
                lineEnd = -1;
                return false;
            }

            lineStart = this.currentIndex;
            var currentSubString = this.text[this.currentIndex..];
            int lineFeedStart = currentSubString.IndexOf('\r');
            int lineFeedCharacters = 1;
            if (lineFeedStart == -1)
            {
                lineFeedStart = this.text[this.currentIndex..].IndexOf('\n');
                if (lineFeedStart == -1)
                {
                    line = ReadOnlySpan<char>.Empty;
                    lineStart = -1;
                    lineEnd = -1;
                    return false;
                }
                int lineFeedEnd = lineFeedStart;
            }
            else
            {
                if (lineFeedStart + 1 < currentSubString.Length && currentSubString[lineFeedStart + 1] == '\n')
                    lineFeedCharacters = 2;
            }

            lineEnd = lineStart + lineFeedStart;
            this.currentIndex += lineFeedStart + lineFeedCharacters;
            line = currentSubString[0..lineFeedStart];
            return true;
        }

    }

    public enum SideNoteType
    {
        Undefined,
        Beispiel,
        SpielleiterInformation,
        Optional,
        Information,
    }
}
