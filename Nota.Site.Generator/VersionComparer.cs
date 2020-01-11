using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace Nota.Site.Generator
{
    internal class VersionComparer : IComparable<VersionComparer>
    {
        private readonly NumberSting id;
        private readonly bool thisIsDraft;
        private readonly bool isVNext;

        public VersionComparer(string id)
        {
            this.thisIsDraft = id.StartsWith("draft/");
            this.isVNext = id == "vNext";


            if (this.thisIsDraft)
                this.id = new NumberSting(id.AsMemory("draft/".Length));
            else
                this.id = new NumberSting(id.AsMemory());

        }

        public int CompareTo([AllowNull] VersionComparer? other)
        {

            if (other is null)
                return -1;

            if (this.thisIsDraft && !other.thisIsDraft)
                return 1;
            if (this.thisIsDraft && other.thisIsDraft)
                return this.id.CompareTo(other.id);
            if (!this.thisIsDraft && other.thisIsDraft)
                return -1;

            if (this.isVNext && other.isVNext)
                return 0;
            if (this.isVNext)
                return -1;

            return this.id.CompareTo(other.id);
        }


        private class NumberSting : IComparable<NumberSting>
        {
            private readonly ReadOnlyCollection<object> data;

            public NumberSting(ReadOnlyMemory<char> str)
            {
                int start = 0;
                bool isDigit = char.IsDigit(str.Span[0]);
                var list = new List<object>();
                for (int i = 1; i < str.Length; i++)
                {
                    bool currentIsDigit = char.IsDigit(str.Span[i]);
                    if (currentIsDigit == isDigit)
                        continue;

                    if (isDigit)
                    {
                        var number = int.Parse(str.Slice(start, i - start).Span);
                        list.Add(number);
                    }
                    else
                    {
                        var memory = str.Slice(start, i - start);
                        list.Add(memory);
                    }
                    isDigit = currentIsDigit;
                    start = i;
                }
                this.data = list.AsReadOnly();
            }

            public int CompareTo([AllowNull] NumberSting other)
            {

                for (int i = 0; i < Math.Min(this.data.Count, other.data.Count); i++)
                {
                    var thisElement = this.data[i];
                    var otherElement = other.data[i];

                    if (thisElement is ReadOnlyMemory<char> && otherElement is int)
                        return 1;
                    else if (thisElement is int && otherElement is ReadOnlyMemory<char>)
                        return -1;

                    else if (thisElement is int thisI && otherElement is int otherI)
                    {
                        var c = thisI.CompareTo(otherI);
                        if (c != 0)
                            return c;
                    }

                    else if (thisElement is ReadOnlyMemory<char> thisStr && otherElement is ReadOnlyMemory<char> otherStr)
                    {
                        var c = thisStr.Span.CompareTo(otherStr.Span, StringComparison.InvariantCulture);
                        if (c != 0)
                            return c;
                    }
                    else
                        throw new InvalidOperationException("This should not happen");
                }

                return this.data.Count.CompareTo(other.data.Count);

            }

        }



    }
}