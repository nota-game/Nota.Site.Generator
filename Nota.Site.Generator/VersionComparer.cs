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
            thisIsDraft = id.StartsWith("draft/");
            isVNext = id == "vNext";


            if (thisIsDraft) {
                this.id = new NumberSting(id.AsMemory("draft/".Length));
            } else {
                this.id = new NumberSting(id.AsMemory());
            }
        }

        public int CompareTo([AllowNull] VersionComparer? other)
        {

            if (other is null) {
                return -1;
            }

            if (thisIsDraft && !other.thisIsDraft) {
                return 1;
            }

            if (thisIsDraft && other.thisIsDraft) {
                return id.CompareTo(other.id);
            }

            if (!thisIsDraft && other.thisIsDraft) {
                return -1;
            }

            if (isVNext && other.isVNext) {
                return 0;
            }

            if (isVNext) {
                return -1;
            }

            return id.CompareTo(other.id);
        }


        private class NumberSting : IComparable<NumberSting>
        {
            private readonly ReadOnlyCollection<object> data;

            public NumberSting(ReadOnlyMemory<char> str)
            {
                int start = 0;
                bool isDigit = char.IsDigit(str.Span[0]);
                var list = new List<object>();
                for (int i = 1; i < str.Length; i++) {
                    bool currentIsDigit = char.IsDigit(str.Span[i]);
                    if (currentIsDigit == isDigit) {
                        continue;
                    }

                    if (isDigit) {
                        int number = int.Parse(str.Slice(start, i - start).Span);
                        list.Add(number);
                    } else {
                        ReadOnlyMemory<char> memory = str.Slice(start, i - start);
                        list.Add(memory);
                    }
                    isDigit = currentIsDigit;
                    start = i;
                }
                data = list.AsReadOnly();
            }

            public int CompareTo([AllowNull] NumberSting other)
            {

                if (other is null) {
                    return 1;
                }

                for (int i = 0; i < Math.Min(data.Count, other.data.Count); i++) {
                    object thisElement = data[i];
                    object otherElement = other.data[i];

                    if (thisElement is ReadOnlyMemory<char> && otherElement is int) {
                        return 1;
                    } else if (thisElement is int && otherElement is ReadOnlyMemory<char>) {
                        return -1;
                    } else if (thisElement is int thisI && otherElement is int otherI) {
                        int c = thisI.CompareTo(otherI);
                        if (c != 0) {
                            return c;
                        }
                    } else if (thisElement is ReadOnlyMemory<char> thisStr && otherElement is ReadOnlyMemory<char> otherStr) {
                        int c = thisStr.Span.CompareTo(otherStr.Span, StringComparison.InvariantCulture);
                        if (c != 0) {
                            return c;
                        }
                    } else {
                        throw new InvalidOperationException("This should not happen");
                    }
                }

                return data.Count.CompareTo(other.data.Count);

            }

        }



    }
}