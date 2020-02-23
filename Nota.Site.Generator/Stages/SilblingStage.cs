using Nota.Site.Generator.Stages;
using Stasistium.Core;
using Stasistium.Documents;
using Stasistium.Stages;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nota.Site.Generator.Stages
{
    public class SilblingStage<T, TPreviousItemCache, TPreviousCache> : MultiStageBase<T, string, SlblingCache<TPreviousCache>>
            where TPreviousCache : class
            where TPreviousItemCache : class
    {
        private readonly MultiStageBase<T, TPreviousItemCache, TPreviousCache> input;

        public SilblingStage(MultiStageBase<T, TPreviousItemCache, TPreviousCache> input, IGeneratorContext context, string? name = null) : base(context, name)
        {
            this.input = input ?? throw new ArgumentNullException(nameof(input));
        }


        protected override async Task<StageResultList<T, string, SlblingCache<TPreviousCache>>> DoInternal(SlblingCache<TPreviousCache>? cache, OptionToken options)
        {

            var result = await this.input.DoIt(cache?.PreviousCache, options);

            var task = LazyTask.Create(async () =>
            {

                var performed = await result.Perform;

                var list = await Task.WhenAll(Enumerable.Range(0, performed.Count)
                .Select(async i =>
                {
                    var previous = i > 0 ? performed[i - 1].Id : null;
                    var next = i < performed.Count - 1 ? performed[i + 1].Id : null;
                    var current = performed[i];

                    string? lastHash = null;
                    bool orderChanged = true;
                    if (cache != null)
                    {
                        var lastPosition = (cache.IdOrder as IList<string>).IndexOf(current.Id);
                        if (lastPosition > -1)
                        {
                            lastHash = cache.HashOrder[lastPosition];
                            var lastPrevious = lastPosition > 0 ? cache.IdOrder[i - 1] : null;
                            var lastNext = lastPosition < cache.IdOrder.Length - 1 ? cache.IdOrder[i + 1] : null;

                            orderChanged = lastPrevious != previous || lastNext != next;
                        }
                    }

                    var subTask = LazyTask.Create(async () =>
                    {
                        var subPerform = await current.Perform;
                        return subPerform.With(subPerform.Metadata.Add(new SilblingMetadata(previous, next)));
                    });

                    string hash;
                    if (orderChanged || current.HasChanges)
                    {
                        var t = await subTask;
                        hash = t.Hash;
                    }
                    else
                    {
                        // cache is null if orderChanged was false
                        hash = cache!.HashOrder[i];
                    }

                    return StageResult.CreateStageResult(this.Context, subTask, hash != lastHash, current.Id, hash, hash);

                }));

                return list.ToImmutableList();
            });

            bool hasChanges = result.HasChanges;
            ImmutableList<string> ids;
            SlblingCache<TPreviousCache> newCache;

            if (hasChanges || cache is null)
            {
                var t = await task;
                newCache = new SlblingCache<TPreviousCache>()
                {

                    PreviousCache = result.Cache,
                    IdOrder = t.Select(x => x.Id).ToArray(),
                    HashOrder = t.Select(x => x.Cache).ToArray(),
                    Hash = this.Context.GetHashForObject(t.Select(x => x.Hash)),
                };
                ids = newCache.IdOrder.ToImmutableList();
            }
            else
            {
                newCache = cache;
                ids = cache.IdOrder.ToImmutableList();
            }


            return this.Context.CreateStageResultList(task, hasChanges, ids, newCache, newCache.Hash, result.Cache);
        }
    }



    public class SlblingCache<TPreviousCache> : IHavePreviousCache<TPreviousCache>
        where TPreviousCache : class
    {
        public TPreviousCache PreviousCache { get; set; }

        public string[] IdOrder { get; set; }
        public string[] HashOrder { get; set; }
        public string Hash { get; set; }
    }
}

namespace Nota.Site.Generator
{
    public static partial class StageExtensions
    {
        public static SilblingStage<T, TPreviousItemCache, TPreviousCache> Silblings<T, TPreviousItemCache, TPreviousCache>(this MultiStageBase<T, TPreviousItemCache, TPreviousCache> input, string? name = null)
            where TPreviousCache : class
            where TPreviousItemCache : class
        {
            return new SilblingStage<T, TPreviousItemCache, TPreviousCache>(input, input.Context, name);
        }
    }

    public class SilblingMetadata
    {
        public SilblingMetadata(string? previous, string? next)
        {
            this.Previous = previous;
            this.Next = next;
        }

        public string? Next { get; }
        public string? Previous { get; }
    }
}
