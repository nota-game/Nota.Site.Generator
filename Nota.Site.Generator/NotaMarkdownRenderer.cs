using Microsoft.Toolkit.Parsers.Markdown.Blocks;
using Microsoft.Toolkit.Parsers.Markdown.Inlines;
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
                    builder.Append($"<div class=\"edit-box\"><div class=\"edit-lable\"><a href=\"{sourceReferenceBlock.OriginalDocument.Id}\" >Bearbeiten</a></div>");
                    this.Render(builder, sourceReferenceBlock.Blocks);
                    builder.Append("</div>");

                    break;

                case Markdown.Blocks.SideNote blocks:
                    builder.Append($"<aside id=\"{blocks.Id}\" class=\"{blocks.SideNoteType}{(blocks.Distributions.Any() ? " " : string.Empty)}{string.Join(" ", blocks.Distributions.Select(x => $"{x.id}-{x.distribution}"))}\" >");
                    builder.Append($"<h1>{blocks.SideNoteType}</h1>");
                    this.Render(builder, blocks.Blocks);
                    builder.Append("</aside>");

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