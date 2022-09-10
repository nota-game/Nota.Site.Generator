using AdaptMark.Parsers.Markdown;
using Nota.Site.Generator.Markdown.Blocks;
using Stasistium;
using Stasistium.Documents;
using Stasistium.Stages;
using System.Linq;
using System.Threading.Tasks;
namespace Nota.Site.Generator.Markdown.Blocks
{
    public class YamlMarkdownToDocumentMetadataStage<T> : StageBaseSimple<MarkdownDocument, MarkdownDocument>
        where T : class
    {
        public YamlMarkdownToDocumentMetadataStage(IGeneratorContext context, string? name) : base(context, name)
        {
        }

        protected override Task<IDocument<MarkdownDocument>> Work(IDocument<MarkdownDocument> input, OptionToken options)
        {
            var block = input.Value.Blocks.OfType<YamlBlock<T>>().FirstOrDefault();
            if (block is null)
                return Task.FromResult(input);
            var newDocument = input.Value.GetBuilder().Build();
            newDocument.Blocks = input.Value.Blocks;
            _ = newDocument.Blocks.Remove(block);
            var result = input.With(input.Metadata.Add(block.Data));
            return Task.FromResult(result);
        }
    }
}
