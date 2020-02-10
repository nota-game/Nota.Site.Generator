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
        public SideNote(string id, SideNoteType sideNoteType, IEnumerable<(string id, byte distribution)> distributions, IEnumerable<MarkdownBlock> blocks)
        {
            if (distributions is null)
                throw new ArgumentNullException(nameof(distributions));
            if (blocks is null)
                throw new ArgumentNullException(nameof(blocks));
            this.Id = id ?? throw new ArgumentNullException(nameof(id));
            this.SideNoteType = sideNoteType;
            this.Distributions = distributions.ToImmutableArray();
            this.Blocks = blocks.ToImmutableArray();
        }

        public string Id { get; }
        public SideNoteType SideNoteType { get; }

        public ImmutableArray<(string id, byte distribution)> Distributions { get; }
        public ImmutableArray<MarkdownBlock> Blocks { get; }

        public new class Parser : Parser<SideNote>
        {

            private readonly Regex distributionPatter = new Regex(@"(?<id>\S+)\s+(?<distribution>\d)", RegexOptions.Compiled);

            protected override BlockParseResult<SideNote>? ParseInternal(string markdown, int startOfLine, int firstNonSpace, int endOfFirstLine, int maxStart, int maxEnd, bool lineStartsNewParagraph, MarkdownDocument document)
            {
                if (markdown[startOfLine] != '|')
                    return null;

                var lines = new LineSplitter(markdown.AsSpan(startOfLine, maxEnd - startOfLine));
                //var distributionList = ImmutableArray<>
                var builder = ImmutableArray<(string id, byte distribution)>.Empty.ToBuilder();
                string? id = null;
                SideNoteType? type = null;
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
                        if (id == null)
                        {
                            id = text.ToString();
                        }
                        else if (!type.HasValue)
                        {
                            if (!Enum.TryParse<SideNoteType>(text.ToString(), out var parsed))
                                parsed = SideNoteType.Undefined;
                            type = parsed;
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

                var blocks = document.ParseBlocks(buffer.ToString(), 0, buffer.Length, out _);
                var result = new SideNote(id ?? string.Empty, type ?? SideNoteType.Undefined, builder, blocks);

                return BlockParseResult.Create(result, startOfLine, lastend + startOfLine);
            }


        }

        public override string ToString()
        {
            var builder = new StringBuilder();

            if (this.Id != null)
                WriteLine(this.Id);
            WriteLine(this.SideNoteType.ToString());
            foreach (var (id, value) in this.Distributions)
            {
                Write(id);
                builder.Append(' ');
                builder.Append(value);
                builder.AppendLine();
            }

            builder.AppendLine("|---");
            var splitter = new LineSplitter(string.Join("\n\n", this.Blocks));
            while (splitter.TryGetNextLine(out var line, out _, out _))
            {
                WriteLine(line);
            }

            void WriteLine(ReadOnlySpan<char> txt)
            {
                Write(txt);
                builder.AppendLine();
            }
            void Write(ReadOnlySpan<char> txt)
            {
                builder.Append("| ");
                builder.Append(txt);
            }


            return builder.ToString();
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
                    line = this.text[this.currentIndex..];
                    lineStart = this.currentIndex;
                    lineEnd = this.currentIndex + line.Length;
                    this.currentIndex = lineEnd;
                    return true;
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
