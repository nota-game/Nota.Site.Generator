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
                    builder.Append($"<div class=\"{sourceReferenceBlock.OriginalDocument.Id}\">");
                    this.Render(builder, sourceReferenceBlock.Blocks);
                    builder.Append("</div>");

                    break;

                case Markdown.Blocks.SideNote blocks:
                    builder.Append($"<div id=\"{blocks.Reference}\" class=\"{blocks.SideNoteType}{(blocks.Distributions.Any() ? " " : string.Empty)}{string.Join(" ", blocks.Distributions.Select(x => $"{x.id}-{x.distribution}"))}\" >");
                    this.Render(builder, blocks.Blocks);
                    builder.Append("</div>");

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