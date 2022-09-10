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

        public string? EditUrl { get; }

        public NotaMarkdownRenderer(string? editUrl)
        {
            EditUrl = editUrl;
        }

        protected override void Render(StringBuilder builder, MarkdownBlock block)
        {
            switch (block) {
                case Markdown.Blocks.SoureReferenceBlock sourceReferenceBlock:
                    _ = builder.Append($"<div class=\"edit-box\">");
                    GitRefMetadata? reffData = sourceReferenceBlock.OriginalDocument.Metadata.TryGetValue<GitRefMetadata>();
                    Book? book = sourceReferenceBlock.OriginalDocument.Metadata.TryGetValue<Book>();
                    _ = builder.Append($"<span class='edit-info' >");
                    if (EditUrl is not null && reffData is not null && book is not null) {

                        _ = builder.Append($"<a href=\"");
                        _ = builder.Append(EditUrl);
                        if (!EditUrl.EndsWith("/")) {
                            _ = builder.Append('/');
                        }

                        _ = builder.Append(reffData.Name);
                        _ = builder.Append("/books/");
                        _ = builder.Append(book.Name);
                        _ = builder.Append('/');

                        _ = builder.Append(sourceReferenceBlock.OriginalDocument.Id);
                        _ = builder.Append("\" target=\"_blank\" >Bearbeiten</a>");
                    }
                    GitMetadata? commitDetails = sourceReferenceBlock.OriginalDocument.Metadata.TryGetValue<GitMetadata>();
                    if (commitDetails != null) {
                        DateTimeOffset date = commitDetails.FileCommits.First().Author.Date;

                        _ = builder.Append("<span class=\"timecode\" timecode=\"");
                        _ = builder.Append(date
                            .Subtract(new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero))
                            .TotalMilliseconds);
                        _ = builder.Append("\" >");
                        _ = builder.Append(date.ToString());
                        _ = builder.Append("</span>");
                    }
                    _ = builder.Append("</span>");
                    Render(builder, sourceReferenceBlock.Blocks);
                    _ = builder.Append("</div>");

                    break;

                case Markdown.Blocks.ChapterHeaderBlock header:
                    _ = builder.Append("<h");
                    _ = builder.Append(header.HeaderLevel.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    string id = header.ChapterId ?? GetHeaderText(header);
                    id = id.Replace(' ', '-');
                    if (id.Length > 0) {
                        _ = builder.Append(" id=\"");
                        _ = builder.Append(id);
                        _ = builder.Append("\" ");
                    }
                    _ = builder.Append(">");

                    foreach (MarkdownInline item in header.Inlines) {
                        Render(builder, item);
                    }

                    _ = builder.Append("</h");
                    _ = builder.Append(header.HeaderLevel.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    _ = builder.Append(">");

                    break;

                case Markdown.Blocks.SideNote blocks:
                    _ = builder.Append($"<aside id=\"{blocks.Id}\" class=\"{blocks.SideNoteType}{(blocks.Distributions.Any() ? " " : string.Empty)}{string.Join(" ", blocks.Distributions.Select(x => $"{x.id}-{x.distribution}"))}\" >");
                    Render(builder, blocks.Blocks);
                    _ = builder.Append("</aside>");

                    break;

                case Markdown.Blocks.DivBlock div:
                    _ = builder.Append($"<div class=\"{div.CssClass}\" >");
                    Render(builder, div.Blocks);
                    _ = builder.Append("</div>");
                    break;

                case Markdown.Blocks.ExtendedTableBlock table:
                    _ = builder.Append("<table>");

                    if (table.HasHeader) {
                        _ = builder.Append("<thead>");
                        PrintRows(0, 1, true);
                        _ = builder.Append("</thead>");
                        _ = builder.Append("<tbody>");
                        PrintRows(1, table.Rows.Count, false);
                        _ = builder.Append("</tbody>");
                    } else {
                        _ = builder.Append("<tbody>");
                        PrintRows(0, table.Rows.Count, false);
                        _ = builder.Append("</tbody>");
                    }

                    void PrintRows(int from, int to, bool header)
                    {
                        for (int i = from; i < to; i++) {
                            _ = builder.Append("<tr>");
                            for (int j = 0; j < table.Rows[i].Cells.Count; j++) {
                                if (i == 0 && table.HasHeader) {
                                    _ = builder.Append("<th>");
                                } else {
                                    _ = builder.Append("<td>");
                                }

                                Render(builder, table.Rows[i].Cells[j].Inlines);

                                if (i == 0 && table.HasHeader) {
                                    _ = builder.Append("</th>");
                                } else {
                                    _ = builder.Append("</td>");
                                }
                            }
                            _ = builder.Append("</tr>");
                        }
                    }

                    _ = builder.Append("</table>");
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