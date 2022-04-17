using AdaptMark.Markdown.Blocks;
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

    public class DivBlock : MarkdownBlock, IBlockContainer
    {
        public DivBlock(string cssClass, IEnumerable<MarkdownBlock> blocks)
        {
            if (blocks is null)
                throw new ArgumentNullException(nameof(blocks));
            this.CssClass = cssClass;
            this.Blocks = blocks.ToImmutableArray();
        }


        public string CssClass { get; }

        public ImmutableArray<MarkdownBlock> Blocks { get; }
        IReadOnlyList<MarkdownBlock> IBlockContainer.Blocks => this.Blocks.AsReadonly();

        public new class Parser : Parser<DivBlock>
        {

            protected override BlockParseResult<DivBlock>? ParseInternal(in LineBlock markdown, int startLine, bool lineStartsNewParagraph, MarkdownDocument document)
            {
                var firstLine = markdown[startLine];
                var startOfTable = firstLine.IndexOfNonWhiteSpace();
                if (!IsBeginOfTag(firstLine)) {
                    return null;
                }

var tmp = markdown.ToString();
                int openingTags = 0;
                int? closingLine = null;
                // search closing tag
                for (int i = 1; i < markdown.LineCount; i++) {
                    var currentLine = markdown[i];
                    if (IsBeginOfTag(currentLine)) {
                        openingTags++;
                    }
                    if (IsEndOfTag(currentLine)) {
                        if (openingTags > 0) {
                            openingTags--;
                        } else {
                            closingLine = i;
                            break;
                        }
                    }
                }

                if (!closingLine.HasValue) {
                    return null;
                }

                var cssClass = firstLine[3..].Trim();

                var blocks = document.ParseBlocks(markdown.SliceLines(1, closingLine.Value-1));


                return BlockParseResult.Create(new DivBlock(cssClass.IsEmpty ? "" : cssClass.ToString(), blocks), startLine, closingLine.Value + 1);


                static bool IsBeginOfTag(ReadOnlySpan<char> firstLine)
                {
                    var startOfText = firstLine.IndexOfNonWhiteSpace();
                    return startOfText != -1
                        &&  startOfText +3  <=firstLine.Length
                        && firstLine[startOfText] == ':'
                        && firstLine[startOfText+1] == ':'
                        && firstLine[startOfText+2] == ':';                    
                }
                static bool IsEndOfTag(ReadOnlySpan<char> firstLine)
                {
                    var startOfText = firstLine.IndexOfNonWhiteSpace();
                    return startOfText != -1
                        &&  startOfText +3  <=firstLine.Length
                        && firstLine[startOfText] == '/'
                        && firstLine[startOfText+1] == ':'
                        && firstLine[startOfText+2] == ':';
                }


            }


        }

        protected override string StringRepresentation()
        {
            var builder = new StringBuilder();

            builder.Append(":::");

            if (this.CssClass is not null) {
                builder.Append(" ");
                builder.Append(this.CssClass);
            }

            bool first = true;
            foreach (var b in this.Blocks) {
                if (!first) {
                    builder.Append("\n\n");
                }
                builder.AppendLine(b.ToString());
                first = false;
            }
            builder.Append("/::");
            return builder.ToString();
        }
    }


}
