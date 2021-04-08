using Stasistium.Documents;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Linq;
using Stasistium.Stages;

namespace Stasistium.Stages
{

    public class IfStage<TInput, TResult> : StageBase<TInput, TResult>
    {
        private readonly Func<IDocument<TInput>, bool> predicates;
        private readonly SubPipline<TInput, TResult> trueP;
        private readonly SubPipline<TInput, TResult> falseP;
        private readonly IStageBaseOutput<TResult> falseResult;
        private readonly IStageBaseOutput<TResult> trueResult;

        public IfStage(Func<IDocument<TInput>, bool> predicates, Func<IStageBaseOutput<TInput>, IStageBaseOutput<TResult>> piplinesTrue, Func<IStageBaseOutput<TInput>, IStageBaseOutput<TResult>> piplinesFalse, IGeneratorContext context, string? name = null) : base(context, name)
        {
            this.predicates = predicates;
            (this.trueP, this.trueResult) = SubPipeline.Create(piplinesTrue, this.Context);
            (this.falseP, this.falseResult) = SubPipeline.Create(piplinesFalse, this.Context);
        }


        protected override async Task<ImmutableList<IDocument<TResult>>> Work(ImmutableList<IDocument<TInput>> input, OptionToken options)
        {
            var trueDocuments = input.Where(this.predicates).ToImmutableList();
            var falseDocuments = input.Except(trueDocuments).ToImmutableList();

            var builder = ImmutableList.CreateBuilder<IDocument<TResult>>();
            this.falseResult.PostStages += Result_PostStages;
            this.trueResult.PostStages += Result_PostStages;


            Task Result_PostStages(ImmutableList<IDocument<TResult>> input, OptionToken resultOptions)
            {
                if (options == resultOptions)
                    builder.AddRange(input);
                return Task.CompletedTask;
            }

            await Task.WhenAll(this.trueP.Invoke(trueDocuments, options),
                this.falseP.Invoke(falseDocuments, options));
            this.falseResult.PostStages -= Result_PostStages;
            this.trueResult.PostStages -= Result_PostStages;

            return builder.ToImmutable();
        }

    }
    public class IfStage<TInput, TResult, TAditionalData> : StageBase<TInput, TAditionalData, TResult>
    {

        private readonly Func<IDocument<TInput>, bool> predicates;
        private readonly SubPipline<TInput, TInput> trueIn;
        private readonly SubPipline<TInput, TInput> falseIn;
        private readonly SubPipline<TAditionalData, TAditionalData> additionalIn;

        private readonly IStageBaseOutput<TResult> falseResult;
        private readonly IStageBaseOutput<TResult> trueResult;

        //private readonly StageBase<TInput, TInputCache> input;

        public IfStage(Func<IDocument<TInput>, bool> predicates, Func<IStageBaseOutput<TInput>, IStageBaseOutput<TAditionalData>, IStageBaseOutput<TResult>> piplinesTrue, Func<IStageBaseOutput<TInput>, IStageBaseOutput<TAditionalData>, IStageBaseOutput<TResult>> piplinesFalse, IGeneratorContext context, string? name = null) : base(context, name)
        {
            this.predicates = predicates;

            this.trueIn = SubPipeline.Create<TInput>(this.Context);
            this.falseIn = SubPipeline.Create<TInput>(this.Context);
            this.additionalIn = SubPipeline.Create<TAditionalData>(this.Context);

            this.trueResult = piplinesTrue(this.trueIn, this.additionalIn);
            this.falseResult = piplinesFalse(this.falseIn, this.additionalIn);
        }


        protected override async Task<ImmutableList<IDocument<TResult>>> Work(ImmutableList<IDocument<TInput>> input, ImmutableList<IDocument<TAditionalData>> additionalData, OptionToken options)
        {
            options = options.CreateSubToken();
            var trueDocuments = input.Where(this.predicates).ToImmutableList();
            var falseDocuments = input.Except(trueDocuments).ToImmutableList();

            var builder = ImmutableList.CreateBuilder<IDocument<TResult>>();
            this.falseResult.PostStages += Result_PostStages;
            this.trueResult.PostStages += Result_PostStages;

            Task Result_PostStages(ImmutableList<IDocument<TResult>> input, OptionToken resultOptions)
            {
                if (options == resultOptions)
                    builder.AddRange(input);
                return Task.CompletedTask;
            }

            await Task.WhenAll(
                this.trueIn.Invoke(trueDocuments, options),
                this.falseIn.Invoke(falseDocuments, options),
                this.additionalIn.Invoke(additionalData, options)
                );
            this.falseResult.PostStages -= Result_PostStages;
            this.trueResult.PostStages -= Result_PostStages;

            return builder.ToImmutable();
        }

    }
    public class If2Stage<TInput, TResult, TAditionalData> : StageBase<TInput, TAditionalData, TResult>
    {

        private readonly Func<IDocument<TInput>, bool> predicates;
        private readonly SubPipline<TInput, TInput> trueIn;
        private readonly SubPipline<TInput, TInput> falseIn;
        private readonly SubPipline<TAditionalData, TAditionalData> additionalIn;

        private readonly IStageBaseOutput<TResult> falseResult;
        private readonly IStageBaseOutput<TResult> trueResult;

        //private readonly StageBase<TInput, TInputCache> input;

        public If2Stage(Func<IDocument<TInput>, bool> predicates, Func<IStageBaseOutput<TInput>, IStageBaseOutput<TAditionalData>, IStageBaseOutput<TResult>> piplinesTrue, Func<IStageBaseOutput<TInput>, IStageBaseOutput<TAditionalData>, IStageBaseOutput<TResult>> piplinesFalse, IGeneratorContext context, string? name = null) : base(context, name)
        {
            this.predicates = predicates;

            this.trueIn = SubPipeline.Create<TInput>(this.Context);
            this.falseIn = SubPipeline.Create<TInput>(this.Context);
            this.additionalIn = SubPipeline.Create<TAditionalData>(this.Context);

            this.trueResult = piplinesTrue(this.trueIn, this.additionalIn);
            this.falseResult = piplinesFalse(this.falseIn, this.additionalIn);
        }


        protected override async Task<ImmutableList<IDocument<TResult>>> Work(ImmutableList<IDocument<TInput>> input, ImmutableList<IDocument<TAditionalData>> additionalData, OptionToken options)
        {
            options = options.CreateSubToken();
            var trueDocuments = input.Where(this.predicates).ToImmutableList();
            var falseDocuments = input.Except(trueDocuments).ToImmutableList();

            var builder = ImmutableList.CreateBuilder<IDocument<TResult>>();
            this.falseResult.PostStages += Result_PostStages;
            this.trueResult.PostStages += Result_PostStages;

            Task Result_PostStages(ImmutableList<IDocument<TResult>> input, OptionToken resultOptions)
            {
                if (options == resultOptions)
                    builder.AddRange(input);
                return Task.CompletedTask;
            }

            var truetask = this.trueIn.Invoke(trueDocuments, options);
            var falsetask = this.falseIn.Invoke(falseDocuments, options);
            var additionaltask = this.additionalIn.Invoke(additionalData, options);
            await Task.Delay(2000);

            await Task.WhenAll(
                truetask,
                falsetask,
                additionaltask
                );
            this.falseResult.PostStages -= Result_PostStages;
            this.trueResult.PostStages -= Result_PostStages;

            return builder.ToImmutable();
        }

    }
}

namespace Stasistium
{
    public static partial class StageExtensions2
    {


        public static IfHelper1<TInput> If<TInput>(this IStageBaseOutput<TInput> stage, Func<IDocument<TInput>, bool> predicates, string? name = null)
        {
            return new IfHelper1<TInput>(stage, predicates, name);
        }

        public static IfHelper1<TInput, TAditionalData> If<TInput, TAditionalData>(this IStageBaseOutput<TInput> stage, IStageBaseOutput<TAditionalData> additional, Func<IDocument<TInput>, bool> predicates, string? name = null)
        {
            return new IfHelper1<TInput, TAditionalData>(stage, additional, predicates, name);
        }
        public static IfHelper12<TInput, TAditionalData> If2<TInput, TAditionalData>(this IStageBaseOutput<TInput> stage, IStageBaseOutput<TAditionalData> additional, Func<IDocument<TInput>, bool> predicates, string? name = null)
        {
            return new IfHelper12<TInput, TAditionalData>(stage, additional, predicates, name);
        }


        public class IfHelper12<TInput, TAditionalData>

        {
            private readonly string? name;
            private readonly IStageBaseOutput<TInput> stage;
            private readonly IStageBaseOutput<TAditionalData> additional;
            private readonly Func<IDocument<TInput>, bool> predicates;

            public IfHelper12(IStageBaseOutput<TInput> stage, IStageBaseOutput<TAditionalData> additional, Func<IDocument<TInput>, bool> predicates, string? name)
            {
                this.stage = stage;
                this.additional = additional;
                this.predicates = predicates;
                this.name = name;
            }

            public IfHelper22<TInput, TAditionalData, TResult> Then<TResult>(Func<IStageBaseOutput<TInput>, IStageBaseOutput<TAditionalData>, IStageBaseOutput<TResult>> piplinesTrue)
            {
                return new IfHelper22<TInput, TAditionalData, TResult>(this.stage, this.additional, this.predicates, piplinesTrue, this.name);
            }

        }
        public class IfHelper22<TInput, TAditionalData, TResult>
        {
            private readonly string? name;
            private readonly IStageBaseOutput<TInput> stage;
            private readonly IStageBaseOutput<TAditionalData> additional;
            private readonly Func<IDocument<TInput>, bool> predicates;
            private readonly Func<IStageBaseOutput<TInput>, IStageBaseOutput<TAditionalData>, IStageBaseOutput<TResult>> piplinesTrue;

            public IfHelper22(IStageBaseOutput<TInput> stage, IStageBaseOutput<TAditionalData> additional, Func<IDocument<TInput>, bool> predicates, Func<IStageBaseOutput<TInput>, IStageBaseOutput<TAditionalData>, IStageBaseOutput<TResult>> piplinesTrue, string? name)
            {
                this.stage = stage;
                this.additional = additional;
                this.predicates = predicates;
                this.piplinesTrue = piplinesTrue;
                this.name = name;
            }
            public IStageBaseOutput<TResult> Else(Func<IStageBaseOutput<TInput>, IStageBaseOutput<TAditionalData>, IStageBaseOutput<TResult>> piplinesFalse)
            {
                return If2StageExtension.If2(this.stage, this.additional, this.predicates, this.piplinesTrue, piplinesFalse, this.name);
            }
        }
        public class IfHelper1<TInput, TAditionalData>

        {
            private readonly string? name;
            private readonly IStageBaseOutput<TInput> stage;
            private readonly IStageBaseOutput<TAditionalData> additional;
            private readonly Func<IDocument<TInput>, bool> predicates;

            public IfHelper1(IStageBaseOutput<TInput> stage, IStageBaseOutput<TAditionalData> additional, Func<IDocument<TInput>, bool> predicates, string? name)
            {
                this.stage = stage;
                this.additional = additional;
                this.predicates = predicates;
                this.name = name;
            }

            public IfHelper2<TInput, TAditionalData, TResult> Then<TResult>(Func<IStageBaseOutput<TInput>, IStageBaseOutput<TAditionalData>, IStageBaseOutput<TResult>> piplinesTrue)
            {
                return new IfHelper2<TInput, TAditionalData, TResult>(this.stage, this.additional, this.predicates, piplinesTrue, this.name);
            }

        }
        public class IfHelper2<TInput, TAditionalData, TResult>
        {
            private readonly string? name;
            private readonly IStageBaseOutput<TInput> stage;
            private readonly IStageBaseOutput<TAditionalData> additional;
            private readonly Func<IDocument<TInput>, bool> predicates;
            private readonly Func<IStageBaseOutput<TInput>, IStageBaseOutput<TAditionalData>, IStageBaseOutput<TResult>> piplinesTrue;

            public IfHelper2(IStageBaseOutput<TInput> stage, IStageBaseOutput<TAditionalData> additional, Func<IDocument<TInput>, bool> predicates, Func<IStageBaseOutput<TInput>, IStageBaseOutput<TAditionalData>, IStageBaseOutput<TResult>> piplinesTrue, string? name)
            {
                this.stage = stage;
                this.additional = additional;
                this.predicates = predicates;
                this.piplinesTrue = piplinesTrue;
                this.name = name;
            }
            public IStageBaseOutput<TResult> Else(Func<IStageBaseOutput<TInput>, IStageBaseOutput<TAditionalData>, IStageBaseOutput<TResult>> piplinesFalse)
            {
                return IfStageExtension.If(this.stage, this.additional, this.predicates, this.piplinesTrue, piplinesFalse, this.name);
            }
        }
        public class IfHelper1<TInput>

        {
            private readonly string? name;
            private readonly IStageBaseOutput<TInput> stage;
            private readonly Func<IDocument<TInput>, bool> predicates;

            public IfHelper1(IStageBaseOutput<TInput> stage, Func<IDocument<TInput>, bool> predicates, string? name)
            {
                this.stage = stage;
                this.predicates = predicates;
                this.name = name;
            }

            public IfHelper2<TInput, TResult> Then<TResult>(Func<IStageBaseOutput<TInput>, IStageBaseOutput<TResult>> piplinesTrue)

            {
                return new IfHelper2<TInput, TResult>(this.stage, this.predicates, piplinesTrue, this.name);
            }

        }
        public class IfHelper2<TInput, TResult>
        {
            private readonly string? name;
            private readonly IStageBaseOutput<TInput> stage;
            private readonly Func<IDocument<TInput>, bool> predicates;
            private readonly Func<IStageBaseOutput<TInput>, IStageBaseOutput<TResult>> piplinesTrue;

            public IfHelper2(IStageBaseOutput<TInput> stage, Func<IDocument<TInput>, bool> predicates, Func<IStageBaseOutput<TInput>, IStageBaseOutput<TResult>> piplinesTrue, string? name)
            {
                this.stage = stage;
                this.predicates = predicates;
                this.piplinesTrue = piplinesTrue;
                this.name = name;
            }
            public IStageBaseOutput<TResult> Else(Func<IStageBaseOutput<TInput>, IStageBaseOutput<TResult>> piplinesFalse)
            {
                return IfStageExtension.If(this.stage, this.predicates, this.piplinesTrue, piplinesFalse, this.name);
            }
        }

        //public static MultiStageBase<TIn, string, ConcatStageManyCache<WhereStageCache<TInCache>, TSecondOutCache>> Branch<TIn, TInItemCache, TInCache, TSecondOutItemCache, TSecondOutCache>(this MultiStageBase<TIn, TInItemCache, TInCache> input, Predicate<IDocument<TIn>> predicate, Func<MultiStageBase<TIn, TInItemCache, WhereStageCache<TInCache>>, MultiStageBase<TIn, TSecondOutItemCache, TSecondOutCache>> branch)
        //    where TInItemCache : class
        //    where TInCache : class
        //    where TSecondOutItemCache : class
        //    where TSecondOutCache : class
        //{

        //    var truePath = branch(input.Where(x => predicate(x)));
        //    var falsePath = input.Where(x => !predicate(x));

        //    return falsePath.Concat(truePath);
        //}

        //public static MultiStageBase<TOut, string, ConcatStageManyCache<TThirdOutCache, TSecondOutCache>> Branch<TIn, TOut, TInItemCache, TInCache, TSecondOutItemCache, TSecondOutCache, TThirdOutItemCache, TThirdOutCache>(this MultiStageBase<TIn, TInItemCache, TInCache> input, Predicate<IDocument<TIn>> predicate, Func<MultiStageBase<TIn, TInItemCache, WhereStageCache<TInCache>>, MultiStageBase<TOut, TSecondOutItemCache, TSecondOutCache>> branch, Func<MultiStageBase<TIn, TInItemCache, WhereStageCache<TInCache>>, MultiStageBase<TOut, TThirdOutItemCache, TThirdOutCache>> elseBranch)
        //    where TThirdOutItemCache : class
        //    where TThirdOutCache : class
        //    where TInItemCache : class
        //    where TInCache : class
        //    where TSecondOutItemCache : class
        //    where TSecondOutCache : class
        //{

        //    var truePath = branch(input.Where(x => predicate(x)));
        //    var falsePath = elseBranch(input.Where(x => !predicate(x)));

        //    return falsePath.Concat(truePath);
        //}


    }
}