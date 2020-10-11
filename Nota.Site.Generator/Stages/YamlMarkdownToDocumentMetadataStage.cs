using AdaptMark.Parsers.Markdown;
using Nota.Site.Generator.Markdown.Blocks;
using Stasistium;
using Stasistium.Documents;
using Stasistium.Stages;
using System.Linq;
using System.Threading.Tasks;
namespace Nota.Site.Generator.Markdown.Blocks
{
    public class YamlMarkdownToDocumentMetadataStage<T, TCache> : Stasistium.Stages.GeneratedHelper.Single.Simple.OutputSingleInputSingleSimple1List0StageBase<MarkdownDocument, TCache, MarkdownDocument>
        where TCache : class
        where T : class
    {
        public YamlMarkdownToDocumentMetadataStage(StageBase<MarkdownDocument, TCache> inputSingle0, IGeneratorContext context, string? name) : base(inputSingle0, context, name)
        {
        }

        protected override Task<IDocument<MarkdownDocument>> Work(IDocument<MarkdownDocument> input, OptionToken options)
        {
            var block = input.Value.Blocks.OfType<YamlBlock<T>>().FirstOrDefault();
            if (block is null)
                return Task.FromResult(input);
            var newDocument = input.Value.GetBuilder().Build();
            newDocument.Blocks = input.Value.Blocks;
            newDocument.Blocks.Remove(block);
            var result = input.With(input.Metadata.Add(block.Data));
            return Task.FromResult(result);
        }
    }
}
namespace Nota.Site.Generator
{
    public static partial class StageExtensions
    {
        public static YamlMarkdownToDocumentMetadataHelper<TCache> YamlMarkdownToDocumentMetadata<TCache>(this StageBase<MarkdownDocument, TCache> input, string? name = null)
            where TCache : class
        {
            return new YamlMarkdownToDocumentMetadataHelper<TCache>(input, name);
        }

        public class YamlMarkdownToDocumentMetadataHelper<TCache>
        where TCache : class
        {
            private StageBase<MarkdownDocument, TCache> input;
            private string? name;

            public YamlMarkdownToDocumentMetadataHelper(StageBase<MarkdownDocument, TCache> input, string? name)
            {
                this.input = input;
                this.name = name;
            }

            public YamlMarkdownToDocumentMetadataStage<T, TCache> For<T>()
                where T : class
            {
                return new YamlMarkdownToDocumentMetadataStage<T, TCache>(this.input, this.input.Context, this.name);
            }
        }
    }
}