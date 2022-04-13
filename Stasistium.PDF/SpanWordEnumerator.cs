using System;
using System.Linq;

namespace Stasistium.PDF
{

    public ref struct SpanWordEnumerator
    {
        private Range current;
        private readonly ReadOnlySpan<char> buffer;
        private static readonly string WHITESPACE_CHARACTERS = System.Linq.Enumerable.Range(0, 255).Select(x => (char)x).Where(char.IsWhiteSpace).ToString();
        private readonly string? splitCharacters;

        internal SpanWordEnumerator(ReadOnlySpan<char> buffer, string? splitCharacters=null)
        {
            this.buffer = buffer.Trim();
            this.current = new Range(0, 0);
            this.splitCharacters = splitCharacters;
        }

        /// <summary>
        /// Gets the line at the current position of the enumerator.
        /// </summary>
        public ReadOnlySpan<char> Current => buffer[current];
        public Range CurrentRange => current;
        public ReadOnlySpan<char> FromStartIncludingCurrent => buffer[..current.End];
        public ReadOnlySpan<char> FromStartExcludingCurrent => buffer[..current.Start].TrimEnd();
        public ReadOnlySpan<char> FromEndIncludingCurrent => buffer[current.Start..];
        public ReadOnlySpan<char> FromEndExcludingCurrent => buffer[current.End..].TrimStart();

        /// <summary>
        /// Returns this instance as an enumerator.
        /// </summary>
        public SpanWordEnumerator GetEnumerator() => this;

        /// <summary>
        /// Advances the enumerator to the next line of the span.
        /// </summary>
        /// <returns>
        /// True if the enumerator successfully advanced to the next line; false if
        /// the enumerator has advanced past the end of the span.
        /// </returns>
        public bool MoveNext()
        {
            int endOfOldString = current.End.GetOffset(buffer.Length);
            if(endOfOldString>=buffer.Length)
                return false;
            var stride = 0;
            while (endOfOldString + stride < buffer.Length && splitCharacters == null? char.IsWhiteSpace(buffer[endOfOldString + stride]): splitCharacters.Contains(buffer[endOfOldString + stride]))
            {
                stride++;
            }
            int beginningOfNewString = endOfOldString + stride;

            var endOfString = buffer[beginningOfNewString..].IndexOfAny(splitCharacters?? WHITESPACE_CHARACTERS);
            if (endOfString == -1)
            {
                current = ^0..^0;
                return false;
            }
            current = beginningOfNewString..endOfString;

            return true;
        }
        public bool MovePrevious()
        {
            int startOfOldString = current.Start.GetOffset(buffer.Length);
            var stride = 0;
            while (startOfOldString - stride > 0 && splitCharacters == null ? char.IsWhiteSpace(buffer[startOfOldString - stride]) : splitCharacters.Contains(buffer[startOfOldString - stride]))
            {
                stride++;
            }
            int endOfNewString = startOfOldString - stride;

            var beginningOfString = buffer[..endOfNewString].LastIndexOfAny(splitCharacters ?? WHITESPACE_CHARACTERS);
            if (beginningOfString == -1)
            {
                current = 0..0;
                return false;
            }
            current = beginningOfString..endOfNewString;

            return true;
        }
    }
}
