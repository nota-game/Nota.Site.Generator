using Microsoft.Toolkit.Parsers.Markdown;
using Microsoft.Toolkit.Parsers.Markdown.Blocks;
using Microsoft.Toolkit.Parsers.Markdown.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static Microsoft.Toolkit.Parsers.Markdown.Blocks.TableBlock;

namespace Nota.Site.Generator.Markdown.Blocks
{
    public class ExtendedTableBlock : TableBlock
    {
        public bool HasHeader { get; set; }

        public new class Parser : MarkdownBlock.Parser<ExtendedTableBlock>
        {
            protected override BlockParseResult<ExtendedTableBlock>? ParseInternal(string markdown, int startOfLine, int firstNonSpace, int endOfFirstLine, int maxStart, int maxEnd, bool lineStartsNewParagraph, MarkdownDocument document)
            {
                if (markdown[startOfLine] != '+')
                    return null;

                int numberOfcolumns = 0;
                int currentPosition = startOfLine + 1;

                do
                {
                    var nextPosition = markdown.IndexOf('+', currentPosition);
                    numberOfcolumns++;
                    currentPosition = nextPosition + 1;
                } while (currentPosition != -1 && currentPosition < endOfFirstLine);

                var splitter = new LineSplitter(markdown.AsSpan(startOfLine, maxEnd - startOfLine));

                var table = new ExtendedTableBlock
                {
                     
                    ColumnDefinitions = Enumerable.Range(0, numberOfcolumns).Select(x => new TableColumnDefinition()).ToArray(),
                    Rows = new List<TableRow>(),
                };

                if (!splitter.TryGetNextLine(out _, out _, out _))
                    return null;


                var cellText = Enumerable.Range(0, numberOfcolumns).Select(x => new StringBuilder()).ToArray();


                var lastlineEnd = startOfLine;
                while (splitter.TryGetNextLine(out var line, out var lineStart, out var lineEnd))
                {
                    int index = 0;
                    if (line.StartsWith("+"))
                    {
                        // new row
                        var row = new TableRow
                        {
                            Cells = Enumerable.Range(0, cellText.Length).Select(x => new TableCell()).ToArray()
                        };

                        for (int i = 0; i < cellText.Length; i++)
                        {
                            row.Cells[i].Inlines = document.ParseInlineChildren(cellText[i].ToString(), 0, cellText[i].Length, Array.Empty<Type>());
                            cellText[i].Clear();
                        }
                        table.Rows.Add(row);



                        // handle header alignment
                        if (line.Contains(':') || line.Contains('='))
                        {
                            if (line.Contains('='))
                                table.HasHeader = true;

                            while (line.Length > 0)
                            {
                                line = line.Slice(1);

                                var nextPosition = line.IndexOf('+');
                                if (nextPosition == -1)
                                    break;

                                var currentPart = line.Slice(0, nextPosition);

                                {
                                    var startsWithColumn = currentPart.StartsWith(":");
                                    var endsWithColumn = currentPart.EndsWith(":");

                                    if (startsWithColumn && endsWithColumn)
                                        table.ColumnDefinitions[index].Alignment = ColumnAlignment.Center;
                                    else if (startsWithColumn)
                                        table.ColumnDefinitions[index].Alignment = ColumnAlignment.Left;
                                    else if (endsWithColumn)
                                        table.ColumnDefinitions[index].Alignment = ColumnAlignment.Right;
                                    else
                                        table.ColumnDefinitions[index].Alignment = ColumnAlignment.Unspecified;
                                }

                                line = line.Slice(nextPosition);
                                index++;
                            }
                        }

                    }
                    else if (line.StartsWith("|"))
                    {
                        while (line.Length > 0)
                        {

                            line = line.Slice(1);
                            // new row

                            var nextPosition = line.IndexOf('|');
                            if (nextPosition == -1)
                                break;

                            if (cellText[index].Length > 0)
                                cellText[index].AppendLine();
                            cellText[index].Append(line.Slice(0, nextPosition));


                            line = line.Slice(nextPosition);
                            index++;
                        }
                        if (index < numberOfcolumns)
                            break;
                    }
                    else
                        break;

                    lastlineEnd = lineEnd;
                }


                return BlockParseResult.Create(table, startOfLine, lastlineEnd + startOfLine);
            }
        }
    }
}
