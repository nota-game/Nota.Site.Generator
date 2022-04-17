using AdaptMark.Parsers.Markdown;
using Stasistium.Documents;
using System.Threading.Tasks;
using System.Collections.Immutable;
using Stasistium.Stages;
using Nota.Site.Generator.Stages;
using Nota.Site.Generator.Markdown.Blocks;
using System.Collections.Generic;
using System;
using System.Linq;
using AdaptMark.Markdown.Blocks;

namespace Nota.Site.Generator.Stages
{

    public class DependendUponMetadata
    {
        public string[] DependsOn { get; set; } = Array.Empty<string>();
    }
    public class InsertMarkdownFromStage : StageBase<MarkdownDocument, MarkdownDocument, MarkdownDocument>
    {
        public InsertMarkdownFromStage(IGeneratorContext context, string? name) : base(context, name)
        {
        }

        protected override Task<ImmutableList<IDocument<MarkdownDocument>>> Work(ImmutableList<IDocument<MarkdownDocument>> insertTargets, ImmutableList<IDocument<MarkdownDocument>> insertSource, OptionToken options)
        {
            return Task.FromResult(insertTargets.Select(x =>
            {
                var pathResolver = new RelativePathResolver(x.Id, insertTargets.Select(x => x.Id));
                var resolver = new InserBlockResolver(pathResolver, insertTargets, this.Context);
                var resolvedDocument = resolver.Resolve(x.Value);
                return x.With(resolvedDocument, this.Context.GetHashForString(resolvedDocument.ToString()));
            }).ToImmutableList());
        }
    }



    public class InsertMarkdownStage : StageBase<MarkdownDocument, MarkdownDocument>
    {

        public InsertMarkdownStage(IGeneratorContext context, string? name) : base(context, name)
        {
        }

        protected override Task<ImmutableList<IDocument<MarkdownDocument>>> Work(ImmutableList<IDocument<MarkdownDocument>> input, OptionToken options)
        {
            var distinct = input.Distinct().ToArray();
            if (distinct.Length != input.Count)
            {
                var duplicated = string.Join("\n", input.GroupBy(x => x.Id)
                                        .Select(x => (count: x.Count(), id: x.Key))
                                        .Where(x => x.count > 1)
                                        .Select(x => $"{x.id}: {x.count}"));
                Context.Logger.Error($"Duplicated IDs:\n{duplicated}");
            }


            var performedLookup = distinct.ToDictionary(x => x.Id);

            var list = distinct.Select(p =>
            {
                var pathResolver = new RelativePathResolver(p.Id, input.Select(x => x.Id));
                var resplvedDependencys = BlockInsertDependency.Resolve(this.Context, p.Value, pathResolver, performedLookup);
                var resolver = new InserBlockResolver(pathResolver, resplvedDependencys, this.Context);
                var resolvedDocument = resolver.Resolve(p.Value);
                return p.With(resolvedDocument, this.Context.GetHashForString(resolvedDocument.ToString()));
            }).ToImmutableList();

            return Task.FromResult(list);


        }


        private static class BlockInsertDependency
        {


            public static IEnumerable<IDocument<MarkdownDocument>> Resolve(IGeneratorContext context, MarkdownDocument document, RelativePathResolver pathResolver, Dictionary<string, IDocument<MarkdownDocument>> performed)
            {
                return DeepCopy(context, document.Blocks, pathResolver, performed);
            }

            private static IEnumerable<IDocument<MarkdownDocument>> DeepCopy(IGeneratorContext context, IEnumerable<AdaptMark.Parsers.Markdown.Blocks.MarkdownBlock> blocks, RelativePathResolver pathResolver, Dictionary<string, IDocument<MarkdownDocument>> performed)
            {
                foreach (var item in blocks.SelectMany(b => DeepCopy(context, b, pathResolver, performed)))
                    yield return item;

            }
            private static IEnumerable<IDocument<MarkdownDocument>> DeepCopy(IGeneratorContext context, AdaptMark.Parsers.Markdown.Blocks.MarkdownBlock block, RelativePathResolver pathResolver, Dictionary<string, IDocument<MarkdownDocument>> lookup)
            {
                switch (block)
                {
                    case AdaptMark.Parsers.Markdown.Blocks.ListBlock listBlock:
                        {
                            foreach (var item in listBlock.Items.SelectMany(x => DeepCopy(context, x.Blocks, pathResolver, lookup)))
                                yield return item;
                            break;
                        }


                    case InsertBlock insertBlock:
                        {


                            var resolvedPath = pathResolver[insertBlock.Reference];
                            if (resolvedPath is null)
                            {
                                context.Logger.Error($"Did not found Path { insertBlock.Reference} to insert.");
                                yield break;
                            }
                            if (!lookup.TryGetValue(resolvedPath, out var result))
                                throw new InvalidOperationException($"Did not found stage result with ID {resolvedPath}");

                            var performed = result;
                            yield return performed;
                            foreach (var item in DeepCopy(context, performed.Value.Blocks, pathResolver.ForPath(insertBlock.Reference), lookup))
                                yield return item;

                            break;
                        }



                    case IBlockContainer b:
                        {
                            foreach (var item in DeepCopy(context, b.Blocks, pathResolver, lookup))
                                yield return item;
                            break;
                        }

                    default:
                        yield break;
                }
            }


        }


    }
}

