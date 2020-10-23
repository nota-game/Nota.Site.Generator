using AdaptMark.Parsers.Markdown;
using Stasistium.Documents;
using System.Threading.Tasks;
using System.Collections.Immutable;
using Stasistium.Stages;
using Nota.Site.Generator.Stages;
using Nota.Site.Generator.Markdown.Blocks;
using Stasistium.Core;
using System.Collections.Generic;
using System;
using System.Linq;
using AdaptMark.Parsers.Markdown.Blocks;
using AdaptMark.Parsers.Markdown.Inlines;
using Microsoft.Net.Http.Headers;
using AdaptMark.Markdown.Blocks;

namespace Nota.Site.Generator.Stages
{

    public class GetReferenceLocationStage<TCache> : StageBase<MarkdownDocument, GetReferenceLocationStageCache<TCache>>
       where TCache : class
    {



        private readonly StageBase<MarkdownDocument, TCache> input;

        public GetReferenceLocationStage(StageBase<MarkdownDocument, TCache> input, IGeneratorContext context, string? name = null) : base(context, name)
        {
            this.input = input ?? throw new ArgumentNullException(nameof(input));
        }

        protected override async Task<StageResult<MarkdownDocument, GetReferenceLocationStageCache<TCache>>> DoInternal(GetReferenceLocationStageCache<TCache>? cache, OptionToken options)
        {

            var result = await this.input.DoIt(cache?.PreviousCache, options);

            var task = LazyTask.Create(async () =>
            {
                var docDocument = await result.Perform;

                var headers = new Stack<HeaderBlock>();
                var list = new List<ImageReference>();

                var doc = docDocument.Value;
                var currentDocumentId = docDocument.Id;

                SearchBlocks(doc.Blocks);

                void SearchBlocks(IEnumerable<MarkdownBlock> blocks)
                {
                    foreach (var block in blocks)
                    {
                        if (block is HeaderBlock header)
                        {
                            while (headers.Count > 1 && headers.Peek().HeaderLevel >= header.HeaderLevel)
                                headers.Pop();
                            headers.Push(header);
                        }

                        if (block is IBlockContainer container)
                        {
                            SearchBlocks(container.Blocks);
                        }
                        if (block is IInlineContainer inlines)
                        {
                            SearchInlines(inlines.Inlines);
                        }

                    }
                }
                void SearchInlines(IEnumerable<MarkdownInline> inlines)
                {
                    foreach (var inline in inlines)
                    {
                        switch (inline)
                        {
                            case ImageInline image:
                                {
                                    string headerString;
                                    if (headers.Count > 0)
                                    {
                                        if (headers.Peek() is ChapterHeaderBlock chapterHeaderBlock)
                                        {
                                            headerString = chapterHeaderBlock.ChapterId;
                                        }
                                        else
                                        {
                                            headerString = StichStage<object, object>.GenerateHeaderString(headers);
                                        }
                                    }
                                    else
                                    {
                                        headerString = string.Empty;
                                    }

                                    list.Add(new ImageReference()
                                    {
                                        ReferencedId = image.Url,
                                        Document = currentDocumentId,
                                        Header = headerString
                                    });
                                    break;
                                }
                            case IInlineContainer container:
                                {
                                    SearchInlines(container.Inlines);
                                    break;
                                }
                            default:
                                break;
                        }
                    }
                }

                var data = new ImageReferences() { References = list.OrderBy(x => x.Header).ThenBy(x => x.ReferencedId).ToArray() };

                var document = docDocument.With(docDocument.Metadata.Add(data));

                var cached = new GetReferenceLocationStageCache<TCache>()
                {
                    Hash = document.Hash,
                    PreviousCache = result.Cache,
                    //ReferenceCaches = list.ToDictionary(x => x.Document, x => new ImageReference() { ReferencedId = x.ReferencedId, Document = x.Document, Header = x.Header })
                };


                return (document, cached);
            });

            bool hasChanges = result.HasChanges;
            GetReferenceLocationStageCache<TCache> newCache;

            if (hasChanges || cache is null)
            {
                IDocument<MarkdownDocument> resultDocument;
                (resultDocument, newCache) = await task;



                hasChanges = cache is null
                || cache.Hash != newCache.Hash;

            }
            else
            {
                newCache = cache;
            }


            var actualTask = LazyTask.Create(async () =>
            {
                var temp = await task;
                return temp.document;
            });

            return this.Context.CreateStageResult(actualTask, hasChanges, result.Hash, newCache, newCache.Hash, result.Cache);
        }



    }



    public class ImageReference
    {
        public string ReferencedId { get; set; }
        public string Document { get; set; }
        public string Header { get; set; }
    }

    public class ImageReferences
    {
        public ImageReference[] References { get; set; }
    }

    public class GetReferenceLocationStageCache<TCache> : IHavePreviousCache<TCache>
         where TCache : class

    {
        public TCache PreviousCache { get; set; }


        //public Dictionary<string, ImageReference> ReferenceCaches { get; set; }

        public string Hash { get; set; }
    }



}
namespace Nota.Site.Generator
{
    public static partial class StageExtensions
    {

        public static GetReferenceLocationStage<TCache> GetReferenceLocations<TCache>(this StageBase<MarkdownDocument, TCache> input, string? name = null)
        where TCache : class

        {
            return new GetReferenceLocationStage<TCache>(input, input.Context, name);
        }
    }
}
