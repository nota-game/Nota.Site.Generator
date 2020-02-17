using Microsoft.Toolkit.Parsers.Markdown;
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

namespace Nota.Site.Generator.Stages
{
    public class InsertMarkdownStage<TSingleCache, TListItemCache, TListCache> : Stasistium.Stages.GeneratedHelper.Single.Simple.OutputSingleInputSingleSimple1List1StageBase<MarkdownDocument, TSingleCache, MarkdownDocument, TListItemCache, TListCache, MarkdownDocument>
        where TSingleCache : class
        where TListItemCache : class
        where TListCache : class
    {
        public InsertMarkdownStage(StageBase<MarkdownDocument, TSingleCache> inputSingle0, MultiStageBase<MarkdownDocument, TListItemCache, TListCache> inputList0, IGeneratorContext context, string? name) : base(inputSingle0, inputList0, context, name)
        {
        }

        protected override Task<IDocument<MarkdownDocument>> Work(IDocument<MarkdownDocument> inputSingle0, ImmutableList<IDocument<MarkdownDocument>> inputList0, OptionToken options)
        {
            var pathResolver = new RelativePathResolver(inputSingle0.Id, inputList0.Select(x => x.Id));

            var resolver = new InserBlockResolver(pathResolver, inputList0, this.Context);

            var resolvedDocument = resolver.Resolve(inputSingle0.Value);

            return Task.FromResult(inputSingle0.With(resolvedDocument, this.Context.GetHashForString(resolvedDocument.ToString())));
        }
    }



    public class InsertMarkdownStage<TItemCache, TCache> : MultiStageBase<MarkdownDocument, string, MarkdownInsertCache<TCache>>
        where TCache : class
        where TItemCache : class
    {
        private readonly MultiStageBase<MarkdownDocument, TItemCache, TCache> input;

        public InsertMarkdownStage(MultiStageBase<MarkdownDocument, TItemCache, TCache> input, IGeneratorContext context, string? name) : base(context, name)
        {
            this.input = input;
        }

        protected override async Task<StageResultList<MarkdownDocument, string, MarkdownInsertCache<TCache>>> DoInternal(MarkdownInsertCache<TCache>? cache, OptionToken options)
        {
            var result = await this.input.DoIt(cache?.PreviousCache, options);

            var task = LazyTask.Create(async () =>
            {
                var performed = await result.Perform;
                var performedLookup = performed.ToDictionary(x => x.Id);

                var list = await Task.WhenAll(performed.Select(async p =>
                {
                    var subTask = LazyTask.Create(async () =>
                    {

                        var subPerformed = await p.Perform;

                        var pathResolver = new RelativePathResolver(subPerformed.Id, performed.Select(x => x.Id));
                        var resplvedDependencys = await BlockInsertDependency.Resolve(subPerformed.Value, pathResolver, performedLookup).ToArrayAsync();

                        var resolver = new InserBlockResolver(pathResolver, resplvedDependencys, this.Context);
                        var resolvedDocument = resolver.Resolve(subPerformed.Value);

                        return (result: subPerformed.With(resolvedDocument, this.Context.GetHashForString(resolvedDocument.ToString())), dependend: resplvedDependencys.Select(x => x.Id).ToArray());

                    });

                    string? oldHash = null;
                    if (cache != null && cache.IdToOutputHash.TryGetValue(p.Id, out oldHash))
                    {
                        if (!cache.inputIdToInsertedItesm.TryGetValue(p.Id, out var dependsOnIds))
                            dependsOnIds = Array.Empty<string>();
                        var dependsOnHasChanges = dependsOnIds.Select(x =>
                        {
                            if (!performedLookup.TryGetValue(x, out var lookuped))
                                return true;
                            return lookuped.HasChanges;
                        });
                        if (!p.HasChanges && dependsOnHasChanges.All(hasChanges => !hasChanges))
                        {
                            return (result: this.Context.CreateStageResult(LazyTask.Create(async () => (await subTask).result), false, p.Id, oldHash, oldHash), depndendOn: cache.inputIdToInsertedItesm[p.Id]);
                        }
                    }

                    var (subResult, depndendOn) = await subTask;
                    return (result: this.Context.CreateStageResult(subResult, subResult.Hash != oldHash, subResult.Id, subResult.Hash, subResult.Hash), depndendOn);
                }));

                return list;
            });

            bool hasChanges = result.HasChanges;
            ImmutableList<string> ids;
            MarkdownInsertCache<TCache> newCache;

            if (hasChanges || cache is null)
            {
                var temp = await task;

                newCache = new MarkdownInsertCache<TCache>()
                {
                    DocumentOrder = temp.Select(x => x.result.Id).ToArray(),
                    IdToOutputHash = temp.ToDictionary(x => x.result.Id, x => x.result.Cache),
                    inputIdToInsertedItesm = temp.ToDictionary(x => x.result.Id, x => x.depndendOn),
                    PreviousCache = result.Cache,
                    Hash = this.Context.GetHashForObject(temp.Select(x => x.result.Hash)),
                };

                ids = newCache.DocumentOrder.ToImmutableList();
            }
            else
            {
                newCache = cache;
                ids = newCache.DocumentOrder.ToImmutableList();
            }

            return this.Context.CreateStageResultList(LazyTask.Create(async () => (await task).Select<(StageResult<MarkdownDocument, string> result, string[] depndendOn), StageResult<MarkdownDocument, string>>(x => x.result).ToImmutableList()), hasChanges, ids, newCache, newCache.Hash);
        }


        private static class BlockInsertDependency
        {


            public static IAsyncEnumerable<IDocument<MarkdownDocument>> Resolve(MarkdownDocument document, RelativePathResolver pathResolver, Dictionary<string, StageResult<MarkdownDocument, TItemCache>> performed)
            {
                return DeepCopy(document.Blocks, pathResolver, performed);
            }

            private static async IAsyncEnumerable<IDocument<MarkdownDocument>> DeepCopy(IEnumerable<Microsoft.Toolkit.Parsers.Markdown.Blocks.MarkdownBlock> blocks, RelativePathResolver pathResolver, Dictionary<string, StageResult<MarkdownDocument, TItemCache>> performed)
            {
                await foreach (var item in blocks.ToAsyncEnumerable().SelectMany(b => DeepCopy(b, pathResolver, performed)))
                    yield return item;

            }
            private static async IAsyncEnumerable<IDocument<MarkdownDocument>> DeepCopy(Microsoft.Toolkit.Parsers.Markdown.Blocks.MarkdownBlock block, RelativePathResolver pathResolver, Dictionary<string, StageResult<MarkdownDocument, TItemCache>> lookup)
            {
                switch (block)
                {
                    case Microsoft.Toolkit.Parsers.Markdown.Blocks.ListBlock listBlock:
                        {
                            await foreach (var item in listBlock.Items.ToAsyncEnumerable().SelectMany(x => DeepCopy(x.Blocks, pathResolver, lookup)))
                                yield return item;
                            break;
                        }


                    case InsertBlock insertBlock:
                        {
                            var resolvedPath = pathResolver[insertBlock.Reference] ?? throw new InvalidOperationException($"Did not found Path {insertBlock.Reference}.");
                            if (!lookup.TryGetValue(resolvedPath, out var result))
                                throw new InvalidOperationException($"Did not found stage result with ID {resolvedPath}");

                            var performed = await result.Perform;
                            yield return performed;
                            await foreach (var item in DeepCopy(performed.Value.Blocks, pathResolver, lookup))
                                yield return item;

                            break;
                        }

                    case SoureReferenceBlock soureReferenceBlock:
                        {
                            await foreach (var item in DeepCopy(soureReferenceBlock.Blocks, pathResolver, lookup))
                                yield return item;
                            break;
                        }

                    case SideNote b:
                        {
                            await foreach (var item in DeepCopy(b.Blocks, pathResolver, lookup))
                                yield return item;
                            break;
                        }

                    default:
                        yield break;
                }
            }


        }


    }

    public class MarkdownInsertCache<TCache> where TCache : class
    {
        public TCache PreviousCache { get; set; }
        public Dictionary<string, string[]> inputIdToInsertedItesm { get; set; }
        public Dictionary<string, string> IdToOutputHash { get; set; }
        public string[] DocumentOrder { get; set; }
        public string Hash { get; set; }
    }


}
namespace Nota.Site.Generator
{
    public static partial class StageExtensions
    {
        public static InsertMarkdownStage<TSingleCache, TListItemCache, TListCache> InsertMarkdown<TSingleCache, TListItemCache, TListCache>(this StageBase<MarkdownDocument, TSingleCache> input, MultiStageBase<MarkdownDocument, TListItemCache, TListCache> allDocuments, string? name = null)
        where TSingleCache : class
        where TListItemCache : class
        where TListCache : class
        {
            return new InsertMarkdownStage<TSingleCache, TListItemCache, TListCache>(input, allDocuments, input.Context, name);
        }
        public static InsertMarkdownStage<TItemCache, TCache> InsertMarkdown<TItemCache, TCache>(this MultiStageBase<MarkdownDocument, TItemCache, TCache> input, string? name = null)
        where TItemCache : class
        where TCache : class

        {
            return new InsertMarkdownStage<TItemCache, TCache>(input, input.Context, name);
        }
    }
}
