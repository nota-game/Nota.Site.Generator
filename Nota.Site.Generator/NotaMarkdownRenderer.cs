using AdaptMark.Parsers.Markdown.Blocks;
using AdaptMark.Parsers.Markdown.Inlines;
using Stasistium.Stages;
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
                    builder.Append($"<div class=\"edit-box\"><a href=\"{sourceReferenceBlock.OriginalDocument.Id}\" >Bearbeiten</a>");
                    this.Render(builder, sourceReferenceBlock.Blocks);
                    builder.Append("</div>");

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