using Stasistium.Core;
using Stasistium.Documents;
using Stasistium.Stages;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;

namespace Stasistium.Stages
{
    public class VariableStage
    {

        public class Add<T, T2, TPreviousItemCache, TPreviousCache, TPreviousItemCache2, TPreviousCache2> : MultiStageBase<T, string, VariableCache<TPreviousCache, TPreviousCache2>>
            where TPreviousCache : class
            where TPreviousItemCache : class
            where TPreviousCache2 : class
            where TPreviousItemCache2 : class
        {

            private readonly MultiStageBase<T, TPreviousItemCache, TPreviousCache> input;
            private readonly MultiStageBase<T2, TPreviousItemCache2, TPreviousCache2> input2;

            public Add(MultiStageBase<T, TPreviousItemCache, TPreviousCache> input, MultiStageBase<T2, TPreviousItemCache2, TPreviousCache2> input2, IGeneratorContext context, string? name = null) : base(context, name)
            {
                this.input = input ?? throw new ArgumentNullException(nameof(input));
                this.input2 = input2 ?? throw new ArgumentNullException(nameof(input2));
            }

            protected override async Task<StageResultList<T, string, VariableCache<TPreviousCache, TPreviousCache2>>> DoInternal(VariableCache<TPreviousCache, TPreviousCache2>? cache, OptionToken options)
            {
                var result = await this.input.DoIt(cache?.PreviousCache, options);
                var result2 = await this.input2.DoIt(cache?.PreviousCache2, options);

                var task = LazyTask.Create(async () =>
                {
                    var performed = await result.Perform;

                    var list = await Task.WhenAll(performed.Select(async item =>
                    {
                        var subTask = LazyTask.Create(async () =>
                        {
                            var subResult = await item.Perform;

                            var metadata = new Data<T2, TPreviousItemCache2, TPreviousCache2>(result2.Hash, this.input2);
                            subResult = subResult.With(subResult.Metadata.Add(metadata));
                            return subResult;
                        });

                        bool hasChanges;
                        string? hash = null;
                        string? oldHash = null;
                        if (cache is null || !cache.InputIdToHash.TryGetValue(item.Id, out oldHash) || item.HasChanges || result2.HasChanges)
                        {
                            var subResult = await subTask;
                            hash = subResult.Hash;
                            if (cache != null)
                            {
                                hasChanges = hash != oldHash;
                            }
                            else
                            {
                                hasChanges = true;
                            }
                        }
                        else
                        {
                            hasChanges = false;
                            hash = oldHash;
                        }

                        return this.Context.CreateStageResult(subTask, hasChanges, item.Id, hash, hash);
                    }));
                    return list.ToImmutableList();
                });
                bool hasChanges;
                VariableCache<TPreviousCache, TPreviousCache2> newCache;
                ImmutableList<string> ids;

                if (cache is null || result.HasChanges || result2.HasChanges)
                {
                    var t = await task;
                    newCache = new VariableCache<TPreviousCache, TPreviousCache2>()
                    {
                        IdOrder = t.Select(x => x.Id).ToArray(),
                        InputIdToHash = t.ToDictionary(x => x.Id, x => x.Cache),
                        PreviousCache = result.Cache,
                        PreviousCache2 = result2.Cache,
                        Hash = this.Context.GetHashForObject(t.Select(x => x.Hash)),
                    };
                    ids = newCache.IdOrder.ToImmutableList();

                    hasChanges = cache is null
                        || !cache.IdOrder.SequenceEqual(newCache.IdOrder)
                        || t.Any(x => x.HasChanges);

                }
                else
                {
                    hasChanges = false;
                    newCache = cache;
                    ids = cache.IdOrder.ToImmutableList();
                }


                return this.Context.CreateStageResultList(task, hasChanges, ids, newCache, newCache.Hash);



            }

        }

        public class Add<T, T2, TPreviousItemCache, TPreviousCache, TPreviousCache2> : MultiStageBase<T, string, VariableCache<TPreviousCache, TPreviousCache2>>
         where TPreviousCache : class
         where TPreviousItemCache : class
         where TPreviousCache2 : class
        {

            private readonly MultiStageBase<T, TPreviousItemCache, TPreviousCache> input;
            private readonly StageBase<T2, TPreviousCache2> input2;

            public Add(MultiStageBase<T, TPreviousItemCache, TPreviousCache> input, StageBase<T2, TPreviousCache2> input2, IGeneratorContext context, string? name = null) : base(context, name)
            {
                this.input = input ?? throw new ArgumentNullException(nameof(input));
                this.input2 = input2 ?? throw new ArgumentNullException(nameof(input2));
            }

            protected override async Task<StageResultList<T, string, VariableCache<TPreviousCache, TPreviousCache2>>> DoInternal(VariableCache<TPreviousCache, TPreviousCache2>? cache, OptionToken options)
            {
                var result = await this.input.DoIt(cache?.PreviousCache, options);
                var result2 = await this.input2.DoIt(cache?.PreviousCache2, options);

                var task = LazyTask.Create(async () =>
                {
                    var performed = await result.Perform;

                    var list = await Task.WhenAll(performed.Select(async item =>
                    {
                        var subTask = LazyTask.Create(async () =>
                        {
                            var subResult = await item.Perform;

                            var metadata = new Data<T2, TPreviousCache2>(result2.Hash, this.input2);
                            subResult = subResult.With(subResult.Metadata.Add(metadata));
                            return subResult;
                        });

                        bool hasChanges;
                        string? hash = null;
                        string? oldHash = null;
                        if (cache is null || !cache.InputIdToHash.TryGetValue(item.Id, out oldHash) || item.HasChanges || result2.HasChanges)
                        {
                            var subResult = await subTask;
                            hash = subResult.Hash;
                            if (cache != null)
                            {
                                hasChanges = hash != oldHash;
                            }
                            else
                            {
                                hasChanges = true;
                            }
                        }
                        else
                        {
                            hasChanges = false;
                            hash = oldHash;
                        }

                        return this.Context.CreateStageResult(subTask, hasChanges, item.Id, hash, hash);
                    }));
                    return list.ToImmutableList();
                });
                bool hasChanges;
                VariableCache<TPreviousCache, TPreviousCache2> newCache;
                ImmutableList<string> ids;

                if (cache is null || result.HasChanges || result2.HasChanges)
                {
                    var t = await task;
                    newCache = new VariableCache<TPreviousCache, TPreviousCache2>()
                    {
                        IdOrder = t.Select(x => x.Id).ToArray(),
                        InputIdToHash = t.ToDictionary(x => x.Id, x => x.Cache),
                        PreviousCache = result.Cache,
                        PreviousCache2 = result2.Cache,
                        Hash = this.Context.GetHashForObject(t.Select(x => x.Hash)),
                    };
                    ids = newCache.IdOrder.ToImmutableList();

                    hasChanges = cache is null
                        || !cache.IdOrder.SequenceEqual(newCache.IdOrder)
                        || t.Any(x => x.HasChanges);

                }
                else
                {
                    hasChanges = false;
                    newCache = cache;
                    ids = cache.IdOrder.ToImmutableList();
                }


                return this.Context.CreateStageResultList(task, hasChanges, ids, newCache, newCache.Hash);



            }

        }

        public class Get<TIn, TOut, TPreviousCache, TSubCache, T, TPreviousItemCache2, TPreviousCache2> : StageBase<TOut, GetCache<TPreviousCache, TSubCache>>
            where TPreviousCache : class
            where TSubCache : class
            where TPreviousCache2 : class
            where TPreviousItemCache2 : class
        {
            private readonly StageBase<TIn, TPreviousCache> input;
            private readonly Func<StageBase<TIn, string>, MultiStageBase<T, TPreviousItemCache2, TPreviousCache2>, StageBase<TOut, TSubCache>> piplinesTrue;


            public Get(StageBase<TIn, TPreviousCache> input, Func<StageBase<TIn, string>, MultiStageBase<T, TPreviousItemCache2, TPreviousCache2>, StageBase<TOut, TSubCache>> piplinesTrue, IGeneratorContext context, string? name) : base(context, name)
            {
                this.input = input ?? throw new ArgumentNullException(nameof(input));
                this.piplinesTrue = piplinesTrue ?? throw new ArgumentNullException(nameof(piplinesTrue));
            }

            protected override async Task<StageResult<TOut, GetCache<TPreviousCache, TSubCache>>> DoInternal(GetCache<TPreviousCache, TSubCache>? cache, OptionToken options)
            {
                var result = await this.input.DoIt(cache?.PrviousCache, options);

                var task = LazyTask.Create(async () =>
                {
                    var performed = await result.Perform;
                    var data = performed.Metadata.GetValue<Data<T, TPreviousItemCache2, TPreviousCache2>>();
                    var trueResult = await this.piplinesTrue(new Start(performed, this.Context), data.GetStage()).DoIt(cache?.SubCache, options);
                    var subCache = trueResult.Cache;
                    var resultDocument = await trueResult.Perform;

                    resultDocument.With(resultDocument.Metadata.Remove<Data<T, TPreviousItemCache2, TPreviousCache2>>());

                    return (resultDocument, subCache);
                });

                string documentId;
                bool hasChanges = result.HasChanges;
                GetCache<TPreviousCache, TSubCache> newCache;

                if (hasChanges || cache is null)
                {
                    var (resultDocument, subCache) = await task;

                    newCache = new GetCache<TPreviousCache, TSubCache>()
                    {
                        PrviousCache = result.Cache,
                        SubCache = subCache,
                        DocumentId = resultDocument.Id,
                        Hash = resultDocument.Hash,
                    };
                    documentId = resultDocument.Id;

                    hasChanges = cache is null
                        || cache.Hash != newCache.Hash;

                }
                else
                {
                    newCache = cache;
                    documentId = cache.DocumentId;
                }


                var actualTask = LazyTask.Create(async () =>
                {
                    var temp = await task;
                    return temp.resultDocument;
                });

                return this.Context.CreateStageResult(actualTask, hasChanges, documentId, newCache, newCache.Hash);
            }


            private class Start : StageBase<TIn, string>
            {

                private readonly IDocument<TIn> document;

                public Start(IDocument<TIn> document, IGeneratorContext context, string? name = null) : base(context, name)
                {
                    this.document = document;
                }

                protected override Task<StageResult<TIn, string>> DoInternal(string? cache, OptionToken options)
                {
                    return Task.FromResult(this.Context.CreateStageResult(this.document, this.document.Hash != cache, this.document.Id, this.document.Hash, this.document.Hash));
                }
            }
        }

        public class Get<TIn, TOut, TPreviousCache, TSubCache, T, TPreviousCache2> : StageBase<TOut, GetCache<TPreviousCache, TSubCache>>
      where TPreviousCache : class
      where TSubCache : class
      where TPreviousCache2 : class
        {
            private readonly StageBase<TIn, TPreviousCache> input;
            private readonly Func<StageBase<TIn, string>, StageBase<T, TPreviousCache2>, StageBase<TOut, TSubCache>> piplinesTrue;


            public Get(StageBase<TIn, TPreviousCache> input, Func<StageBase<TIn, string>, StageBase<T, TPreviousCache2>, StageBase<TOut, TSubCache>> piplinesTrue, IGeneratorContext context, string? name) : base(context, name)
            {
                this.input = input ?? throw new ArgumentNullException(nameof(input));
                this.piplinesTrue = piplinesTrue ?? throw new ArgumentNullException(nameof(piplinesTrue));
            }

            protected override async Task<StageResult<TOut, GetCache<TPreviousCache, TSubCache>>> DoInternal(GetCache<TPreviousCache, TSubCache>? cache, OptionToken options)
            {
                var result = await this.input.DoIt(cache?.PrviousCache, options);

                var task = LazyTask.Create(async () =>
                {
                    var performed = await result.Perform;
                    var data = performed.Metadata.GetValue<Data<T, TPreviousCache2>>();
                    var trueResult = await this.piplinesTrue(new Start(performed, this.Context), data.GetStage()).DoIt(cache?.SubCache, options);
                    var subCache = trueResult.Cache;
                    var resultDocument = await trueResult.Perform;

                    resultDocument.With(resultDocument.Metadata.Remove<Data<T, TPreviousCache2>>());

                    return (resultDocument, subCache);
                });

                string documentId;
                bool hasChanges = result.HasChanges;
                GetCache<TPreviousCache, TSubCache> newCache;

                if (hasChanges || cache is null)
                {
                    var (resultDocument, subCache) = await task;

                    newCache = new GetCache<TPreviousCache, TSubCache>()
                    {
                        PrviousCache = result.Cache,
                        SubCache = subCache,
                        DocumentId = resultDocument.Id,
                        Hash = resultDocument.Hash,
                    };
                    documentId = resultDocument.Id;

                    hasChanges = cache is null
                        || cache.Hash != newCache.Hash;

                }
                else
                {
                    newCache = cache;
                    documentId = cache.DocumentId;
                }


                var actualTask = LazyTask.Create(async () =>
                {
                    var temp = await task;
                    return temp.resultDocument;
                });

                return this.Context.CreateStageResult(actualTask, hasChanges, documentId, newCache, newCache.Hash);
            }


            private class Start : StageBase<TIn, string>
            {

                private readonly IDocument<TIn> document;

                public Start(IDocument<TIn> document, IGeneratorContext context, string? name = null) : base(context, name)
                {
                    this.document = document;
                }

                protected override Task<StageResult<TIn, string>> DoInternal(string? cache, OptionToken options)
                {
                    return Task.FromResult(this.Context.CreateStageResult(this.document, this.document.Hash != cache, this.document.Id, this.document.Hash, this.document.Hash));
                }
            }
        }

        private class Data<T, TPreviousItemCache2, TPreviousCache2>
            where TPreviousCache2 : class
            where TPreviousItemCache2 : class
        {
            public string Hash { get; }
            private readonly MultiStageBase<T, TPreviousItemCache2, TPreviousCache2> stage;

            public Data(string hash, MultiStageBase<T, TPreviousItemCache2, TPreviousCache2> stage)
            {
                this.Hash = hash ?? throw new ArgumentNullException(nameof(hash));
                this.stage = stage ?? throw new ArgumentNullException(nameof(stage));
            }

            public MultiStageBase<T, TPreviousItemCache2, TPreviousCache2> GetStage() => this.stage;

            public override string ToString()
            {
                return this.Hash;
            }
        }


        private class Data<T, TPreviousCache2>
    where TPreviousCache2 : class
        {
            public string Hash { get; }
            private readonly StageBase<T, TPreviousCache2> stage;

            public Data(string hash, StageBase<T, TPreviousCache2> stage)
            {
                this.Hash = hash ?? throw new ArgumentNullException(nameof(hash));
                this.stage = stage ?? throw new ArgumentNullException(nameof(stage));
            }

            public StageBase<T, TPreviousCache2> GetStage() => this.stage;

            public override string ToString()
            {
                return this.Hash;
            }
        }

    }

    public class GetCache<TPreviousCache, TPreviousCache2>
        where TPreviousCache : class
        where TPreviousCache2 : class
    {
        public TPreviousCache PrviousCache { get; set; }
        public TPreviousCache2 SubCache { get; set; }
        public string DocumentId { get; set; }
        public string Hash { get; set; }
    }


    public class VariableCache<TPreviousCache, TPreviousCache2>
        where TPreviousCache : class
    {
        public TPreviousCache PreviousCache { get; set; }
        public TPreviousCache2 PreviousCache2 { get; set; }
        public Dictionary<string, string> InputIdToHash { get; set; }
        public string[] IdOrder { get; set; }
        public string Hash { get; set; }
    }

}


namespace Stasistium
{
    public static partial class StageExtensions2
    {


        public static VariableStage.Add<T, T2, TPreviousItemCache, TPreviousCache, TPreviousItemCache2, TPreviousCache2> SetVariable<T, T2, TPreviousItemCache, TPreviousCache, TPreviousItemCache2, TPreviousCache2>(this MultiStageBase<T, TPreviousItemCache, TPreviousCache> input, MultiStageBase<T2, TPreviousItemCache2, TPreviousCache2> set, string? name = null)
             where TPreviousCache : class
            where TPreviousItemCache : class
            where TPreviousCache2 : class
            where TPreviousItemCache2 : class
        {
            return new VariableStage.Add<T, T2, TPreviousItemCache, TPreviousCache, TPreviousItemCache2, TPreviousCache2>(input, set, input.Context, name);
        }

        public static VariableStage.Get<TIn, TOut, TPreviousCache, TSubCache, T, TPreviousItemCache2, TPreviousCache2> GetVariable<TIn, TOut, TPreviousCache, TSubCache, T, TPreviousItemCache2, TPreviousCache2>(this StageBase<TIn, TPreviousCache> input, MultiStageBase<T, TPreviousItemCache2, TPreviousCache2> toGet, Func<StageBase<TIn, string>, MultiStageBase<T, TPreviousItemCache2, TPreviousCache2>, StageBase<TOut, TSubCache>> get, string? name = null)
            where TPreviousCache : class
            where TSubCache : class
            where TPreviousCache2 : class
            where TPreviousItemCache2 : class
        {
            return new VariableStage.Get<TIn, TOut, TPreviousCache, TSubCache, T, TPreviousItemCache2, TPreviousCache2>(input, get, input.Context, name);
        }

        public static VariableStage.Add<T, T2, TPreviousItemCache, TPreviousCache, TPreviousCache2> SetVariable<T, T2, TPreviousItemCache, TPreviousCache, TPreviousCache2>(this MultiStageBase<T, TPreviousItemCache, TPreviousCache> input, StageBase<T2, TPreviousCache2> set, string? name = null)
          where TPreviousCache : class
         where TPreviousItemCache : class
         where TPreviousCache2 : class
        {
            return new VariableStage.Add<T, T2, TPreviousItemCache, TPreviousCache, TPreviousCache2>(input, set, input.Context, name);
        }

        public static VariableStage.Get<TIn, TOut, TPreviousCache, TSubCache, T, TPreviousCache2> GetVariable<TIn, TOut, TPreviousCache, TSubCache, T, TPreviousCache2>(this StageBase<TIn, TPreviousCache> input, StageBase<T, TPreviousCache2> toGet, Func<StageBase<TIn, string>, StageBase<T, TPreviousCache2>, StageBase<TOut, TSubCache>> get, string? name = null)
            where TPreviousCache : class
            where TSubCache : class
            where TPreviousCache2 : class
        {
            return new VariableStage.Get<TIn, TOut, TPreviousCache, TSubCache, T, TPreviousCache2>(input, get, input.Context, name);
        }
    }
}