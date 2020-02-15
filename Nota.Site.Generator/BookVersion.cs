using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace Nota.Site.Generator
{
    public readonly struct BookVersion : IComparable<BookVersion>, IComparable, IEquatable<BookVersion>
    {
        private readonly bool isVNext;
        public BookVersion(bool isDraft, string name) : this(isDraft, false, name)
        {
            if (name == "vNext" && this.IsDraft == false)
                throw new NotSupportedException("A Tag/Version with the name vNext is not supported");
        }

        private BookVersion(bool isDraft, bool isVNect, string name)
        {
            this.IsDraft = isDraft;
            this.isVNext = isVNect;
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public static readonly BookVersion VNext = new BookVersion(true, true, "vNext");

        public bool IsDraft { get; }
        public string Name { get; }

        public override string ToString()
        {
            if (this.IsDraft && !this.isVNext)
                return "draft/" + this.Name;
            return this.Name;
        }

        public int CompareTo([AllowNull] BookVersion other)
        {
            if (this.IsDraft && !other.IsDraft)
                return 1;
            if (this.IsDraft && other.IsDraft)
                return this.CompareNumberString(this.Name.AsSpan(), other.Name.AsSpan());
            if (!this.IsDraft && other.IsDraft)
                return -1;

            if (this.isVNext && other.isVNext)
                return 0;
            if (this.isVNext)
                return -1;

            return this.CompareNumberString(this.Name.AsSpan(), other.Name.AsSpan());
        }

        int IComparable.CompareTo(object? obj)
        {
            if (obj is BookVersion other)
                return this.CompareTo(other);
            throw new ArgumentException($"Value must be instance of {typeof(BookVersion).FullName} but was {obj?.GetType().FullName ?? "null"}.", nameof(obj));
        }


        private int CompareNumberString(ReadOnlySpan<char> x1, ReadOnlySpan<char> x2)
        {

            while (true)
            {

                var currentSection1 = NextSection(x1);
                var currentSection2 = NextSection(x2);

                var isNumber1 = char.IsDigit(currentSection1[0]);
                var isNumber2 = char.IsDigit(currentSection2[0]);

                if (!isNumber1 && isNumber2)
                    return 1;
                if (isNumber1 && !isNumber2)
                    return -1;

                if (isNumber1)
                {
                    var number1 = int.Parse(currentSection1);
                    var number2 = int.Parse(currentSection2);
                    var comparation = number1.CompareTo(number2);
                    if (comparation != 0)
                        return comparation;
                }
                else
                {
                    var comperation = currentSection1.CompareTo(currentSection2, StringComparison.InvariantCultureIgnoreCase);
                    if (comperation != 0)
                        return comperation;
                }
                x1 = x1.Slice(currentSection1.Length);
                x2 = x2.Slice(currentSection2.Length);
                if (x1.Length == 0 && x2.Length == 0)
                    return 0;
                if (x1.Length == 0)
                    return -1;
                if (x2.Length == 0)
                    return 1;
            }

            static ReadOnlySpan<char> NextSection(in ReadOnlySpan<char> str)
            {
                bool isDigit = char.IsDigit(str[0]);

                for (int i = 1; i < str.Length; i++)
                {
                    bool currentIsDigit = char.IsDigit(str[i]);
                    if (currentIsDigit == isDigit)
                        continue;

                    return str.Slice(0, i);

                }
                return str;
            }
        }

        public override bool Equals(object? obj)
        {
            return obj is BookVersion version && this.Equals(version);
        }

        public bool Equals([AllowNull] BookVersion other)
        {
            return this.isVNext == other.isVNext &&
                   this.IsDraft == other.IsDraft &&
                   this.Name == other.Name;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(this.isVNext, this.IsDraft, this.Name);
        }

        public static bool operator ==(BookVersion left, BookVersion right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(BookVersion left, BookVersion right)
        {
            return !(left == right);
        }
    }
}
