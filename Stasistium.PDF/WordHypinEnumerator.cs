using NHyphenator;

using System;

namespace Stasistium.PDF
{
    public ref struct WordHypinEnumerator
    {
        private Range current;
        private readonly ReadOnlySpan<char> buffer;
        private readonly Language language;

        internal WordHypinEnumerator(ReadOnlySpan<char> buffer, Language language)
        {
            this.buffer = buffer.Trim();
            this.current = new Range(0, 0);
            this.language = language;
        }


        public bool MoveNext()
        {
            var hypenator = new Hyphenator(new HyphenatePatternsLoader(this.language), "\u00AD");
            
        //  var   textForRun = hypenator.HyphenateText(buffer); 

throw new NotImplementedException();
        }
    }
}
