using Nota.Site.Generator.Stages;
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
    public class SilblingsStage<T> : StageBase<T, T>
    {

        public SilblingsStage(IGeneratorContext context, string? name = null) : base(context, name)
        {
        }

        protected override Task<ImmutableList<IDocument<T>>> Work(ImmutableList<IDocument<T>> input, OptionToken options)
        {
            var performed = input;

            var list = Enumerable.Range(0, performed.Count)
            .Select(i =>
            {
                var previous = i > 0 ? performed[i - 1].Id : null;
                var next = i < performed.Count - 1 ? performed[i + 1].Id : null;
                var current = performed[i];

                var subPerform = current;
                var subTask = subPerform.With(subPerform.Metadata.Add(new SilblingMetadata(previous, next)));

                return subTask;
            });

            return Task.FromResult(list.ToImmutableList());
        }
    }

}
namespace Nota.Site.Generator
{

    public class SilblingMetadata
    {
        private SilblingMetadata() { }
        public SilblingMetadata(string? previous, string? next)
        {
            this.Previous = previous;
            this.Next = next;
        }

        public string? Next { get; }
        public string? Previous { get; }
    }
}