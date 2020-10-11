using AdaptMark.Parsers.Markdown;
using AdaptMark.Parsers.Markdown.Blocks;
using AdaptMark.Parsers.Markdown.Helpers;
using AdaptMark.Parsers.Markdown.Inlines;
using Nota.Site.Generator.Markdown.Blocks;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Xml;

namespace Nota.Site.Generator.Markdown
{
    public class XmlSelectorBlock : AdaptMark.Parsers.Markdown.Blocks.MarkdownBlock
    {
        public XmlSelectorBlock(XmlInline header, List<MarkdownBlock> blocks)
        {
            this.Header = header;
            this.Blocks = blocks;
        }

        public XmlInline Header { get; }
        public IList<MarkdownBlock> Blocks { get; }

        public new class Parser : Parser<XmlSelectorBlock>
        {
            protected override BlockParseResult<XmlSelectorBlock>? ParseInternal(in LineBlock markdown, int startLine, bool lineStartsNewParagraph, MarkdownDocument document)
            {
                return null;
            }
            //protected override BlockParseResult<XmlSelectorBlock>? ParseInternal(string markdown, int startOfLine, int firstNonSpace, int endOfFirstLine, int maxStart, int maxEnd, bool lineStartsNewParagraph, MarkdownDocument document)
            //{

            //    if (markdown[startOfLine] != '/')
            //        return null;

            //    var lines = new LineSplitter(markdown.AsSpan(startOfLine, maxEnd - startOfLine));
            //    var buffer = new StringBuilder();
            //    bool isHeader = true;
            //    int lastend = 0;
            //    XmlInline? header = null;
            //    while (lines.TryGetNextLine(out var line, out var start, out var end))
            //    {
            //        lastend = end;
            //        if (line.Length == 0 || line[0] != '/')
            //            break;
            //        int textStart = 1;
            //        if (line.Length >= 2 && line[1] == ' ')
            //            textStart = 2;
            //        var text = line[textStart..];

            //        if (isHeader)
            //        {
            //            isHeader = false;

            //            var parsed = document.ParseInlineChildren(line.ToString(), 0, line.Length, document.InlineParsers.ToArray().Where(x => !(x is XmlInline.Parser)).Select(x => x.GetType()));
            //            if (parsed.Count != 1 && parsed.First() is XmlInline h)
            //                header = h;
            //            else
            //            {
            //                return null;
            //            }
            //        }
            //        else
            //        {
            //            buffer.Append(text);
            //            buffer.AppendLine();
            //        }
            //    }
            //    if (header is null)
            //        return null;

            //    var blocks = document.ParseBlocks(buffer.ToString(), 0, buffer.Length, out _);
            //    var result = new XmlSelectorBlock(header, blocks);

            //    return BlockParseResult.Create(result, startOfLine, lastend + startOfLine);
            //}
        }


        protected override string StringRepresentation()
        {
            var builder = new StringBuilder();

            WriteLine(this.Header.ToString());
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
                builder.Append("/ ");
                builder.Append(txt);
            }


            return builder.ToString();
        }

    }

    public class XmlInline : MarkdownInline
    {
        public XmlInline(string? select, string? context, string? id)
        {
            this.Select = select;
            this.Context = context;
            this.Id = id;
        }

        public string? Select { get; }

        public string? Context { get; }

        public string? Id { get; }

        public new class Parser : Parser<XmlInline>
        {
            protected override InlineParseResult<XmlInline>? ParseInternal(in LineBlock markdown, in LineBlockPosition tripPos, MarkdownDocument document, HashSet<Type> ignoredParsers)
            {
                return null;
            }
            //protected override InlineParseResult<XmlInline>? ParseInternal(string markdown, int minStart, int tripPos, int maxEnd, MarkdownDocument document, IEnumerable<Type> ignoredParsers)
            //{
            //    var endOf = markdown.IndexOf('}', tripPos);
            //    if (endOf == -1 && endOf < maxEnd)
            //        return null;

            //    var select = markdown.Substring(tripPos, endOf - tripPos);



            //    string? context = null;
            //    var start = endOf + 1;
            //    if (start < maxEnd && markdown[start] == '(')
            //    {
            //        endOf = markdown.IndexOf(')', start);
            //        if (endOf == -1 && endOf < maxEnd)
            //        {
            //            context = markdown.Substring(start, endOf - start);
            //        }
            //        else
            //            return null;
            //        start = endOf + 1;
            //    }

            //    string? id = null;
            //    if (start < maxEnd && markdown[start] == '[')
            //    {
            //        endOf = markdown.IndexOf(']', start);
            //        if (endOf == -1 && endOf < maxEnd)
            //        {
            //            context = markdown.Substring(start, endOf - start);
            //        }
            //        return null;
            //    }



            //    if (select.Contains('\n', StringComparison.InvariantCulture)
            //        || select.Contains('\r', StringComparison.InvariantCulture)
            //        || (id?.Contains('\n', StringComparison.InvariantCulture) ?? false)
            //        || (id?.Contains('\r', StringComparison.InvariantCulture) ?? false)
            //        || (select?.Contains('\n', StringComparison.InvariantCulture) ?? false)
            //        || (select?.Contains('\r', StringComparison.InvariantCulture) ?? false))
            //    {
            //        return null;
            //    }


            //    return InlineParseResult.Create(new XmlInline(select, context, id), tripPos, endOf);
            //}

            public override ReadOnlySpan<char> TripChar => "{";
        }

        protected override string StringRepresentation()
        {
            return $"{{{this.Select}}}({this.Context ?? string.Empty})[{this.Id ?? string.Empty}]";
        }

    }

}
