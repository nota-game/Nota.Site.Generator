using AdaptMark.Parsers.Markdown;
using AdaptMark.Parsers.Markdown.Blocks;
using AdaptMark.Parsers.Markdown.Helpers;
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

            protected override BlockParseResult<SideNote>? ParseInternal(in LineBlock markdown, int startLine, bool lineStartsNewParagraph, MarkdownDocument document)
            {
                var firstLine = markdown[startLine];
                var startOfTable = firstLine.IndexOfNonWhiteSpace();
                if (startOfTable != -1 && firstLine[startOfTable] != '|')
                    return null;

                int lastend = 0;
                var header = markdown.RemoveFromLine((l, index) =>
                {
                    if (l.Length == 0)
                        return (0, 0, true, true);

                    var start = l.IndexOfNonWhiteSpace();
                    if (start == -1)
                        return (0, 0, true, true);

                    if (l[start] != '|')
                        return (0, 0, true, true);
                    bool hasDash = false;
                    bool allDashOrWhitespaec = true;
                    for (int i = start + 1; i < l.Length; i++)
                    {
                        if (l[i] == '-')
                        {
                            hasDash = true;
                            continue;
                        }

                        if (char.IsWhiteSpace(l[i]))
                            continue;

                        allDashOrWhitespaec = false;
                    }

                    if (allDashOrWhitespaec && hasDash)
                        return (0, 0, true, true);


                    var rest = l.Slice(start + 1);
                    var contentStart = rest.IndexOfNonWhiteSpace() + start + 1;

                    return (contentStart, l.Length - contentStart, false, false);
                });

                if (header.LineCount == 0)
                    return null;

                var rest = markdown.SliceLines(header.LineCount);

                if (rest.LineCount == 0)
                    return null;

                var seperatorLine = rest[0];
                var nonSpaceSeperatorLine = seperatorLine.IndexOfNonWhiteSpace();
                if (nonSpaceSeperatorLine == -1)
                    return null;
                seperatorLine = seperatorLine.Slice(nonSpaceSeperatorLine + 1);

                if (seperatorLine.Length == 0)
                    return null;

                for (int i = 0; i < seperatorLine.Length; i++)
                {
                    if (seperatorLine[i] == '-')
                        continue;

                    if (char.IsWhiteSpace(seperatorLine[i]))
                        continue;

                    return null;
                }

                rest = rest.SliceLines(1);

                var body = rest.RemoveFromLine((l, index) =>
                {
                    if (l.Length == 0)
                        return (0, 0, true, true);

                    var start = l.IndexOfNonWhiteSpace();
                    if (start == -1)
                        return (0, 0, true, true);

                    if (l[start] != '|')
                        return (0, 0, true, true);

                    var rest = l.Slice(start + 1);
                    var contentStart = rest.IndexOfNonWhiteSpace() + start + 1;

                    return (contentStart, l.Length - contentStart, false, false);
                });

                if (!Enum.TryParse<SideNoteType>(header[0].ToString(), true, out var sideNoteType))
                    sideNoteType = SideNoteType.Undefined;

                var builder = ImmutableArray<(string id, byte distribution)>.Empty.ToBuilder();
                string? id = null;


                for (int i = 1; i < header.LineCount; i++)
                {
                    var currentLine = header[i];
                    if (currentLine.Length == 0)
                        return null;
                    if (currentLine[0] == '[')
                    {
                        var closing = currentLine.FindClosingBrace();
                        if (closing == -1)
                            return null;

                        id = currentLine.Slice(1, closing - 1).ToString();
                    }
                    else
                    {
                        var toParse = currentLine;
                        while (true)
                        {
                            var entryStart = toParse.IndexOfNonWhiteSpace();
                            toParse = toParse.Slice(entryStart);

                            var end = toParse.IndexOfNexWhiteSpace();
                            if (end == -1)
                                end = toParse.Length;

                            var entry = toParse.Slice(0, end);

                            var collumnPos = entry.IndexOf(':');

                            if (collumnPos == -1)
                                return null;

                            var firstPart = entry.Slice(0, collumnPos);
                            var seccondPart = entry.Slice(collumnPos + 1);

                            if (!byte.TryParse(seccondPart.ToString(), out var value))
                                return null;

                            builder.Add((firstPart.ToString(), value));

                            toParse = toParse.Slice(end);
                            if (toParse.IsWhiteSpace())
                                break;
                        }
                    }
                }

                var test = body.ToString();
                var otherTest = new LineBlock(test.AsSpan());

                

                for (int i = 0; i < otherTest.LineCount; i++)
                {
                    var originalLine = body[i];
                    var newLine = otherTest[i];

                    var originalLine2 = originalLine.ToString();
                    var newLine2 = newLine.ToString();


                }


                var blocks = document.ParseBlocks(otherTest);
                var blocks2 = document.ParseBlocks(body);
                var result = new SideNote(id ?? string.Empty, sideNoteType, builder.ToImmutable(), blocks);

                return BlockParseResult.Create(result, startLine, header.LineCount + 1 + body.LineCount);
            }


        }

        protected override string StringRepresentation()
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

    public enum SideNoteType
    {
        Undefined,
        Sample,
        GameMaster,
        Optional,
        Information,
    }
}
