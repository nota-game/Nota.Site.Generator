using Stasistium.Documents;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Linq;
using Stasistium.Core;
using Stasistium.Stages;

namespace Stasistium.Stages
{

    public class IfStage<TInput, TInputCache, TCacheTrue, TCacheFalse, TResult> : StageBase<TResult, IfCache<TInputCache, TCacheTrue, TCacheFalse>>
    where TCacheTrue : class
    where TCacheFalse : class
    where TInputCache : class
    {



        private readonly Func<StageBase<TInput, string>, StageBase<TResult, TCacheTrue>> piplinesTrue;
        private readonly Func<StageBase<TInput, string>, StageBase<TResult, TCacheFalse>> piplinesFalse;
        //private readonly Func<StageBase<TInput, StartCache<TInputCache>>, StageBase<TResult, TItemCache>>[] createPiplines;
        private readonly Func<IDocument<TInput>, Task<bool>> predicates;
        private readonly StageBase<TInput, TInputCache> input;

        public IfStage(StageBase<TInput, TInputCache> input, Func<IDocument<TInput>, Task<bool>> predicates, Func<StageBase<TInput, string>, StageBase<TResult, TCacheTrue>> piplinesTrue, Func<StageBase<TInput, string>, StageBase<TResult, TCacheFalse>> piplinesFalse, IGeneratorContext context, string? name = null) : base(context, name)
        {
            this.input = input ?? throw new ArgumentNullException(nameof(input));
            this.predicates = predicates;
            this.piplinesTrue = piplinesTrue;
            this.piplinesFalse = piplinesFalse;
        }

        protected override async Task<StageResult<TResult, IfCache<TInputCache, TCacheTrue, TCacheFalse>>> DoInternal(IfCache<TInputCache, TCacheTrue, TCacheFalse>? cache, OptionToken options)
        {

            var result = await this.input.DoIt(cache?.PreviousCache, options);

            var task = LazyTask.Create(async () =>
            {
                var performed = await result.Perform;

                var isTrue = await this.predicates.Invoke(performed);

                TCacheTrue? trueCache = null;
                TCacheFalse? falseCache = null;
                IDocument<TResult> resultDocument;
                if (isTrue)
                {
                    var trueResult = await this.piplinesTrue(new Start(performed, this.Context)).DoIt(cache?.TrueCache, options);
                    trueCache = trueResult.Cache;
                    resultDocument = await trueResult.Perform;
                }
                else
                {
                    var falseResult = await this.piplinesFalse(new Start(performed, this.Context)).DoIt(cache?.FalseCache, options);
                    falseCache = falseResult.Cache;
                    resultDocument = await falseResult.Perform;
                }

                return (resultDocument, trueCache, falseCache);
            });

            string documentId;
            bool hasChanges = result.HasChanges;
            IfCache<TInputCache, TCacheTrue, TCacheFalse> newCache;

            if (hasChanges || cache is null)
            {
                var (resultDocument, trueCache, falseCache) = await task;

                newCache = new IfCache<TInputCache, TCacheTrue, TCacheFalse>(
                    result.Cache,
                    trueCache,
                    falseCache,
                    resultDocument.Id,
                    resultDocument.Hash
                );
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

            return this.Context.CreateStageResult(actualTask, hasChanges, documentId, newCache, newCache.Hash, result.Cache);
        }


        private class Start : StageBase<TInput, string>
        {

            private readonly IDocument<TInput> document;

            public Start(IDocument<TInput> document, IGeneratorContext context, string? name = null) : base(context, name)
            {
                this.document = document;
            }

            protected override Task<StageResult<TInput, string>> DoInternal(string? cache, OptionToken options)
            {
                return Task.FromResult(StageResult.CreateStageResult(this.Context, this.document, this.document.Hash != cache, this.document.Id, this.document.Hash, this.document.Hash));
            }
        }

    }

    public class IfCache<TInputCache, TCacheTrue, TCacheFalse> : IHavePreviousCache<TInputCache>
        where TInputCache : class
        where TCacheTrue : class
        where TCacheFalse : class
    {
        private IfCache()
        {

        }
        public IfCache(TInputCache prviousCache, TCacheTrue? trueCache, TCacheFalse? falseCache, string documentId, string hash)
        {
            this.PreviousCache = prviousCache ?? throw new ArgumentNullException(nameof(prviousCache));
            this.TrueCache = trueCache;
            this.FalseCache = falseCache;
            this.DocumentId = documentId ?? throw new ArgumentNullException(nameof(documentId));
            this.Hash = hash ?? throw new ArgumentNullException(nameof(hash));
        }

        public TInputCache PreviousCache { get; set; }
        public TCacheTrue? TrueCache { get; set; }
        public TCacheFalse? FalseCache { get; set; }
        public string DocumentId { get; set; }
        public string Hash { get; set; }
    }


}

namespace Stasistium
{
    public static partial class StageExtensions2
    {


        public static IfHelper1<TInput, Cache> If<TInput, Cache>(this StageBase<TInput, Cache> stage, Func<IDocument<TInput>, Task<bool>> predicates, string? name = null)
            where Cache : class
        {
            return new IfHelper1<TInput, Cache>(stage, predicates, name);
        }
        public static IfHelper1<TInput, Cache> If<TInput, Cache>(this StageBase<TInput, Cache> stage, Predicate<IDocument<TInput>> predicates, string? name = null)
            where Cache : class
        {
            return new IfHelper1<TInput, Cache>(stage, x => Task.FromResult(predicates(x)), name);
        }


        public class IfHelper1<TInput, Cache>
            where Cache : class
        {
            private readonly string? name;
            private StageBase<TInput, Cache> stage;
            private Func<IDocument<TInput>, Task<bool>> predicates;

            public IfHelper1(StageBase<TInput, Cache> stage, Func<IDocument<TInput>, Task<bool>> predicates, string? name)
            {
                this.stage = stage;
                this.predicates = predicates;
                this.name = name;
            }

            public IfHelper2<TInput, Cache, TCacheTrue, TResult> Then<TCacheTrue, TResult>(Func<StageBase<TInput, string>, StageBase<TResult, TCacheTrue>> piplinesTrue)
                where TCacheTrue : class
            {
                return new IfHelper2<TInput, Cache, TCacheTrue, TResult>(this.stage, this.predicates, piplinesTrue, this.name);
            }

        }
        public class IfHelper2<TInput, Cache, TCacheTrue, TResult>
            where Cache : class
            where TCacheTrue : class
        {
            private readonly string? name;
            private StageBase<TInput, Cache> stage;
            private Func<IDocument<TInput>, Task<bool>> predicates;
            private Func<StageBase<TInput, string>, StageBase<TResult, TCacheTrue>> piplinesTrue;

            public IfHelper2(StageBase<TInput, Cache> stage, Func<IDocument<TInput>, Task<bool>> predicates, Func<StageBase<TInput, string>, StageBase<TResult, TCacheTrue>> piplinesTrue, string? name)
            {
                this.stage = stage;
                this.predicates = predicates;
                this.piplinesTrue = piplinesTrue;
                this.name = name;
            }
            public IfStage<TInput, Cache, TCacheTrue, TCacheFalse, TResult> Else<TCacheFalse>(Func<StageBase<TInput, string>, StageBase<TResult, TCacheFalse>> piplinesFalse)
                where TCacheFalse : class
            {
                return new IfStage<TInput, Cache, TCacheTrue, TCacheFalse, TResult>(this.stage, this.predicates, this.piplinesTrue, piplinesFalse, this.stage.Context, this.name);
            }
        }

        public static MultiStageBase<TIn, string, ConcatStageManyCache<WhereStageCache<TInCache>, TSecondOutCache>> Branch<TIn, TInItemCache, TInCache, TSecondOutItemCache, TSecondOutCache>(this MultiStageBase<TIn, TInItemCache, TInCache> input, Predicate<IDocument<TIn>> predicate, Func<MultiStageBase<TIn, TInItemCache, WhereStageCache<TInCache>>, MultiStageBase<TIn, TSecondOutItemCache, TSecondOutCache>> branch)
            where TInItemCache : class
            where TInCache : class
            where TSecondOutItemCache : class
            where TSecondOutCache : class
        {

            var truePath = branch(input.Where(x => predicate(x)));
            var falsePath = input.Where(x => !predicate(x));

            return falsePath.Concat(truePath);
        }

        public static MultiStageBase<TOut, string, ConcatStageManyCache<TThirdOutCache, TSecondOutCache>> Branch<TIn, TOut, TInItemCache, TInCache, TSecondOutItemCache, TSecondOutCache, TThirdOutItemCache, TThirdOutCache>(this MultiStageBase<TIn, TInItemCache, TInCache> input, Predicate<IDocument<TIn>> predicate, Func<MultiStageBase<TIn, TInItemCache, WhereStageCache<TInCache>>, MultiStageBase<TOut, TSecondOutItemCache, TSecondOutCache>> branch, Func<MultiStageBase<TIn, TInItemCache, WhereStageCache<TInCache>>, MultiStageBase<TOut, TThirdOutItemCache, TThirdOutCache>> elseBranch)
            where TThirdOutItemCache : class
            where TThirdOutCache : class
            where TInItemCache : class
            where TInCache : class
            where TSecondOutItemCache : class
            where TSecondOutCache : class
        {

            var truePath = branch(input.Where(x => predicate(x)));
            var falsePath = elseBranch(input.Where(x => !predicate(x)));

            return falsePath.Concat(truePath);
        }


    }
}