using AdaptMark.Parsers.Markdown;
using AdaptMark.Parsers.Markdown.Blocks;
using System;
using System.Collections.Generic;
using Stasistium.Documents;
using AdaptMark.Markdown.Blocks;

namespace Nota.Site.Generator.Markdown.Blocks
{
    public class SoureReferenceBlock : MarkdownBlock, IBlockContainer
    {
        public SoureReferenceBlock(IList<MarkdownBlock> blocks, IDocument<MarkdownDocument> originalDocument)
        {
            this.Blocks = blocks ?? throw new ArgumentNullException(nameof(blocks));
            this.OriginalDocument = originalDocument ?? throw new ArgumentNullException(nameof(originalDocument));
        }

        public IList<MarkdownBlock> Blocks { get; set; }
        IReadOnlyList<MarkdownBlock> IBlockContainer.Blocks => this.Blocks.AsReadonly();

        public IDocument<MarkdownDocument> OriginalDocument { get; set; }

        protected override string StringRepresentation()
        {

            return $"~[{this.OriginalDocument.Id}]\n" + string.Join("\n\n", this.Blocks);

        }

    }


}
