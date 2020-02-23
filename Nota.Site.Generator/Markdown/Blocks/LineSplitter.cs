using System;
using System.Collections.Immutable;

namespace Nota.Site.Generator.Markdown.Blocks
{
    public ref struct LineSplitter
    {
        private readonly ReadOnlySpan<char> text;

        private int currentIndex;

        public LineSplitter(ReadOnlySpan<char> readOnlySpan) : this()
        {
            this.text = readOnlySpan;
        }

        public bool TryGetNextLine(out ReadOnlySpan<char> line, out int lineStart, out int lineEnd)
        {
            if (this.currentIndex >= this.text.Length)
            {
                line = ReadOnlySpan<char>.Empty;
                lineStart = -1;
                lineEnd = -1;
                return false;
            }

            lineStart = this.currentIndex;
            var currentSubString = this.text[this.currentIndex..];
            int lineFeedStart = currentSubString.IndexOf('\r');
            int lineFeedCharacters = 1;
            if (lineFeedStart == -1)
            {
                lineFeedStart = this.text[this.currentIndex..].IndexOf('\n');
                if (lineFeedStart == -1)
                {
                    line = this.text[this.currentIndex..];
                    lineStart = this.currentIndex;
                    lineEnd = this.currentIndex + line.Length;
                    this.currentIndex = lineEnd;
                    return true;
                }
                int lineFeedEnd = lineFeedStart;
            }
            else
            {
                if (lineFeedStart + 1 < currentSubString.Length && currentSubString[lineFeedStart + 1] == '\n')
                    lineFeedCharacters = 2;
            }

            lineEnd = lineStart + lineFeedStart;
            this.currentIndex += lineFeedStart + lineFeedCharacters;
            line = currentSubString[0..lineFeedStart];
            return true;
        }

    }
}
