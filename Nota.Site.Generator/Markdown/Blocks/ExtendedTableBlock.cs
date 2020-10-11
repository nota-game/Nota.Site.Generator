using AdaptMark.Parsers.Markdown;
using AdaptMark.Parsers.Markdown.Blocks;
using AdaptMark.Parsers.Markdown.Helpers;
using System;
using System.Collections.Generic;

namespace Nota.Site.Generator.Markdown.Blocks
{
    public class ExtendedTableBlock : TableBlock
    {
        public bool HasHeader { get; set; }

        public new class Parser : MarkdownBlock.Parser<ExtendedTableBlock>
        {
            protected override BlockParseResult<ExtendedTableBlock>? ParseInternal(in LineBlock markdown2, int startLine, bool lineStartsNewParagraph, MarkdownDocument document)
            {
                var markdown = markdown2;
                if (!lineStartsNewParagraph)
                    return null;
                var consumedLines = 0;

                LineBlock GetColumnLines(LineBlock block)
                {
                    return block.RemoveFromLine((l, i) =>
                    {
                        var nonSpace = l.IndexOfNonWhiteSpace();
                        if (nonSpace == -1 || l[nonSpace] != '|')
                            return (0, 0, true, true);

                        return (0, l.Length, false, false);
                    });
                }

                LineBlock GetCell(LineBlock block, int index)
                {
                    bool hadErrors = false;
                    var b = block.RemoveFromLine((l, _) =>
                    {
                        var start = l.IndexOfNonWhiteSpace() - 1;
                        int end = -1;

                        for (int i = 0; i <= index; i++)
                        {
                            start = l.Slice(start + 1).IndexOf('|') + start + 1;
                            end = l.Slice(start + 1).IndexOf('|') + start + 1;
                        }

                        if (start == -1 || end == -1)
                        {
                            hadErrors = true;
                            return (0, 0, true, true);
                        }

                        return (start + 1, end - start - 1, false, false);
                    });

                    return b;
                }

                (TableColumnDefinition[] columns, bool isHeader)? GetHeader(ReadOnlySpan<char> line)
                {
                    var nonSpace = line.IndexOfNonWhiteSpace();
                    if (nonSpace == -1 || line[nonSpace] != '+')
                        return null;

                    line = line.Slice(nonSpace);

                    var columns = new List<TableColumnDefinition>();

                    char lineChar;
                    if (line.IndexOf('=') != -1)
                        lineChar = '=';
                    else
                        lineChar = '-';

                    while (true)
                    {
                        if (line[0] == '+')
                            line = line.Slice(1);

                        var end = line.IndexOf('+');
                        if (end == -1)
                            break;

                        var definition = new TableColumnDefinition();
                        var content = line.Slice(0, end);
                        line = line.Slice(end);
                        if (content.Length > 0 && content[0] == ':')
                            definition.Alignment = ColumnAlignment.Left;
                        if (content.Length > 0 && content[^1] == ':')
                            definition.Alignment |= ColumnAlignment.Right;

                        bool notValid = false;
                        for (int i = 0; i < content.Length; i++)
                        {
                            if (content[i] != lineChar && (content[i] != ':' || (i != 0 && i != content.Length - 1)))
                            {
                                notValid = true;
                                break;
                            }
                        }
                        if (notValid)
                            break;

                        columns.Add(definition);
                    }

                    return (columns.ToArray(), lineChar == '=');
                }

                markdown = markdown.SliceLines(startLine);
                var firstLine = GetHeader(markdown[0]);

                if (firstLine == null)
                    return null;

                markdown = markdown.SliceLines(1);
                consumedLines += 1;

                var firstRow = GetColumnLines(markdown);

                markdown = markdown.SliceLines(firstRow.LineCount);
                consumedLines += firstRow.LineCount;

                if (markdown.LineCount == 0)
                    return null;

                var seccondSeperator = GetHeader(markdown[0]);

                if (seccondSeperator == null || seccondSeperator.Value.columns.Length != firstLine.Value.columns.Length)
                    return null;

                markdown = markdown.SliceLines(1);
                consumedLines += 1;
                var isHeader = seccondSeperator.Value.isHeader;

                var colums = firstLine.Value.columns;
                var rows = new List<TableRow>();
                rows.Add(new TableRow() { Cells = GetCells(firstRow) });

                TableCell[]? GetCells(LineBlock row)
                {
                    var cells = new TableCell[colums.Length];

                    for (int i = 0; i < colums.Length; i++)
                    {
                        var cellText = GetCell(row, i);
                        if (cellText.LineCount != row.LineCount)
                            break; // In this case there was an error.
                        var inlines = document.ParseInlineChildren(cellText, true, true);

                        var tableCell = new TableCell()
                        {
                            Inlines = inlines
                        };
                        cells[i] = tableCell;
                    }

                    if (cells[^1] == null)
                        return null;
                    return cells;
                }

                while (true)
                {

                    var row = GetColumnLines(markdown);
                    markdown = markdown.SliceLines(row.LineCount);
                    consumedLines += row.LineCount;
                    if (markdown.LineCount == 0)
                        break;
                    if (markdown[0].IsWhiteSpace())
                        break;

                    var cells = GetCells(row);

                    var seperator = GetHeader(markdown[0]);

                    if (seperator == null || seperator.Value.columns.Length != firstLine.Value.columns.Length || seperator.Value.isHeader)
                        return null;
                    markdown = markdown.SliceLines(1);
                    consumedLines += 1;

                    var r = new TableRow()
                    {
                        Cells = cells,
                    };

                    rows.Add(r);
                }

                var result = new ExtendedTableBlock()
                {
                    ColumnDefinitions = colums,
                    HasHeader = isHeader,
                    Rows = rows
                };

                return BlockParseResult.Create(result, startLine, consumedLines);
            }
        }
    }
}
