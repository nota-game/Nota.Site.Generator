using AdaptMark.Parsers.Markdown.Blocks;
using AdaptMark.Parsers.Markdown.Inlines;
using Stasistium.Stages;
using System;
using System.Linq;
using System.Text;

namespace Nota.Site.Generator
{
    internal class NotaMarkdownRenderer : MarkdownRenderer
    {

        protected override void Render(StringBuilder builder, MarkdownBlock block)
        {
            switch (block)
            {
                case Markdown.Blocks.SoureReferenceBlock sourceReferenceBlock:
                    builder.Append($"<div class=\"edit-box\"><span><a href=\"");
                    builder.Append(sourceReferenceBlock.OriginalDocument.Id);
                    builder.Append("\" >Bearbeiten</a>");
                    var commitDetails = sourceReferenceBlock.OriginalDocument.Metadata.TryGetValue<GitMetadata>();
                    if (commitDetails != null)
                    {
                        var date = commitDetails.FileCommits.First().Author.Date;

                        builder.Append("<span class=\"timecode\" timecode=\"");
                        builder.Append(date
                            .Subtract(new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero))
                            .TotalMilliseconds);
                        builder.Append("\" >");
                        builder.Append(date.ToString());
                        builder.Append("</span>");
                    }
                    builder.Append("</span>");
                    this.Render(builder, sourceReferenceBlock.Blocks);
                    builder.Append("</div>");

                    break;

                case Markdown.Blocks.ChapterHeaderBlock header:
                    builder.Append("<h");
                    builder.Append(header.HeaderLevel.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    var id = header.ChapterId ?? GetHeaderText(header);
                    id = id.Replace(' ', '-');
                    if (id.Length > 0)
                    {
                        builder.Append(" id=\"");
                        builder.Append(id);
                        builder.Append("\" ");
                    }
                    builder.Append(">");

                    foreach (var item in header.Inlines)
                        this.Render(builder, item);
                    builder.Append("</h");
                    builder.Append(header.HeaderLevel.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    builder.Append(">");

                    break;

                case Markdown.Blocks.SideNote blocks:
                    builder.Append($"<aside id=\"{blocks.Id}\" class=\"{blocks.SideNoteType}{(blocks.Distributions.Any() ? " " : string.Empty)}{string.Join(" ", blocks.Distributions.Select(x => $"{x.id}-{x.distribution}"))}\" >");
                    this.Render(builder, blocks.Blocks);
                    builder.Append("</aside>");

                    break;

                case Markdown.Blocks.ExtendedTableBlock table:
                    builder.Append("<table>");

                    if (table.HasHeader)
                    {
                        builder.Append("<thead>");
                        PrintRows(0, 1, true);
                        builder.Append("</thead>");
                        builder.Append("<tbody>");
                        PrintRows(1, table.Rows.Count, false);
                        builder.Append("</tbody>");
                    }
                    else
                    {
                        builder.Append("<tbody>");
                        PrintRows(0, table.Rows.Count, false);
                        builder.Append("</tbody>");
                    }

                    void PrintRows(int from, int to, bool header)
                    {
                        for (int i = from; i < to; i++)
                        {
                            builder.Append("<tr>");
                            for (int j = 0; j < table.Rows[i].Cells.Count; j++)
                            {
                                if (i == 0 && table.HasHeader)
                                    builder.Append("<th>");
                                else
                                    builder.Append("<td>");

                                this.Render(builder, table.Rows[i].Cells[j].Inlines);

                                if (i == 0 && table.HasHeader)
                                    builder.Append("</th>");
                                else
                                    builder.Append("</td>");
                            }
                            builder.Append("</tr>");
                        }
                    }

                    builder.Append("</table>");
                    break;

                default:
                    base.Render(builder, block);
                    break;
            }

        }

        protected override void Render(StringBuilder builder, MarkdownInline inline)
        {
            base.Render(builder, inline);
        }

    }
}