using AdaptMark.Markdown.Blocks;
using AdaptMark.Parsers.Markdown;
using AdaptMark.Parsers.Markdown.Blocks;
using AdaptMark.Parsers.Markdown.Helpers;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;

namespace Nota.Site.Generator.Markdown.Blocks
{

    public class SideNote : MarkdownBlock, IBlockContainer
    {
        public SideNote(string id, SideNoteType sideNoteType, IEnumerable<(string id, byte distribution)> distributions, IEnumerable<MarkdownBlock> blocks)
        {
            if (distributions is null) {
                throw new ArgumentNullException(nameof(distributions));
            }

            if (blocks is null) {
                throw new ArgumentNullException(nameof(blocks));
            }

            Id = id ?? throw new ArgumentNullException(nameof(id));
            SideNoteType = sideNoteType;
            Distributions = distributions.ToImmutableArray();
            Blocks = blocks.ToImmutableArray();
        }

        public string Id { get; }
        public SideNoteType SideNoteType { get; }

        public ImmutableArray<(string id, byte distribution)> Distributions { get; }
        public ImmutableArray<MarkdownBlock> Blocks { get; }
        IReadOnlyList<MarkdownBlock> IBlockContainer.Blocks => Blocks.AsReadonly();

        public new class Parser : Parser<SideNote>
        {

            private readonly Regex distributionPatter = new Regex(@"(?<id>\S+)\s+(?<distribution>\d)", RegexOptions.Compiled);

            protected override BlockParseResult<SideNote>? ParseInternal(in LineBlock markdown, int startLine, bool lineStartsNewParagraph, MarkdownDocument document)
            {
                ReadOnlySpan<char> firstLine = markdown[startLine];
                int startOfTable = firstLine.IndexOfNonWhiteSpace();
                if (startOfTable != -1 && firstLine[startOfTable] != '|') {
                    return null;
                }

                int lastend = 0;
                LineBlock header = markdown.RemoveFromLine((l, index) =>
                {
                    if (l.Length == 0) {
                        return (0, 0, true, true);
                    }

                    int start = l.IndexOfNonWhiteSpace();
                    if (start == -1) {
                        return (0, 0, true, true);
                    }

                    if (l[start] != '|') {
                        return (0, 0, true, true);
                    }

                    bool hasDash = false;
                    bool allDashOrWhitespaec = true;
                    for (int i = start + 1; i < l.Length; i++) {
                        if (l[i] == '-') {
                            hasDash = true;
                            continue;
                        }

                        if (char.IsWhiteSpace(l[i])) {
                            continue;
                        }

                        allDashOrWhitespaec = false;
                    }

                    if (allDashOrWhitespaec && hasDash) {
                        return (0, 0, true, true);
                    }

                    ReadOnlySpan<char> rest = l.Slice(start + 1);
                    int contentStart = rest.IndexOfNonWhiteSpace() + start + 1;

                    return (contentStart, l.Length - contentStart, false, false);
                });

                if (header.LineCount == 0) {
                    return null;
                }

                LineBlock rest = markdown.SliceLines(header.LineCount);

                if (rest.LineCount == 0) {
                    return null;
                }

                ReadOnlySpan<char> seperatorLine = rest[0];
                int nonSpaceSeperatorLine = seperatorLine.IndexOfNonWhiteSpace();
                if (nonSpaceSeperatorLine == -1) {
                    return null;
                }

                seperatorLine = seperatorLine.Slice(nonSpaceSeperatorLine + 1);

                if (seperatorLine.Length == 0) {
                    return null;
                }

                for (int i = 0; i < seperatorLine.Length; i++) {
                    if (seperatorLine[i] == '-') {
                        continue;
                    }

                    if (char.IsWhiteSpace(seperatorLine[i])) {
                        continue;
                    }

                    return null;
                }

                rest = rest.SliceLines(1);

                LineBlock body = rest.RemoveFromLine((l, index) =>
                {
                    if (l.Length == 0) {
                        return (0, 0, true, true);
                    }

                    int start = l.IndexOfNonWhiteSpace();
                    if (start == -1) {
                        return (0, 0, true, true);
                    }

                    if (l[start] != '|') {
                        return (0, 0, true, true);
                    }

                    ReadOnlySpan<char> rest = l.Slice(start + 1);

                    int contentStart = rest.IndexOfNonWhiteSpace() == -1
                    ? start + 1
                    : rest.IndexOfNonWhiteSpace() + start + 1;

                    return (contentStart, l.Length - contentStart, false, false);
                });

                if (!Enum.TryParse<SideNoteType>(header[0].ToString(), true, out SideNoteType sideNoteType)) {
                    sideNoteType = SideNoteType.Undefined;
                }

                ImmutableArray<(string id, byte distribution)>.Builder builder = ImmutableArray<(string id, byte distribution)>.Empty.ToBuilder();
                string? id = null;


                for (int i = 1; i < header.LineCount; i++) {
                    ReadOnlySpan<char> currentLine = header[i];
                    if (currentLine.Length == 0) {
                        return null;
                    }

                    if (currentLine[0] == '[') {
                        int closing = currentLine.FindClosingBrace();
                        if (closing == -1) {
                            return null;
                        }

                        id = currentLine.Slice(1, closing - 1).ToString();
                    } else {
                        ReadOnlySpan<char> toParse = currentLine;
                        while (true) {
                            int entryStart = toParse.IndexOfNonWhiteSpace();
                            toParse = toParse.Slice(entryStart);

                            int end = toParse.IndexOfNexWhiteSpace();
                            if (end == -1) {
                                end = toParse.Length;
                            }

                            ReadOnlySpan<char> entry = toParse.Slice(0, end);

                            int collumnPos = entry.IndexOf(':');

                            if (collumnPos == -1) {
                                return null;
                            }

                            ReadOnlySpan<char> firstPart = entry.Slice(0, collumnPos);
                            ReadOnlySpan<char> seccondPart = entry.Slice(collumnPos + 1);

                            if (!byte.TryParse(seccondPart.ToString(), out byte value)) {
                                return null;
                            }

                            builder.Add((firstPart.ToString(), value));

                            toParse = toParse.Slice(end);
                            if (toParse.IsWhiteSpace()) {
                                break;
                            }
                        }
                    }
                }


                List<MarkdownBlock> blocks = document.ParseBlocks(body);
                SideNote result = new SideNote(id ?? string.Empty, sideNoteType, builder.ToImmutable(), blocks);

                return BlockParseResult.Create(result, startLine, header.LineCount + 1 + body.LineCount);
            }


        }

        protected override string StringRepresentation()
        {
            StringBuilder builder = new StringBuilder();

            if (Id != null) {
                WriteLine(Id);
            }

            WriteLine(SideNoteType.ToString());
            foreach ((string id, byte value) in Distributions) {
                Write(id);
                builder.Append(' ');
                builder.Append(value);
                builder.AppendLine();
            }

            builder.AppendLine("|---");
            LineSplitter splitter = new LineSplitter(string.Join("\n\n", Blocks));
            while (splitter.TryGetNextLine(out ReadOnlySpan<char> line, out _, out _)) {
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
