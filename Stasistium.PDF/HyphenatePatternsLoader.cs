using NHyphenator;
using NHyphenator.Loaders;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Stasistium.PDF
{
    internal class HyphenatePatternsLoader : IHyphenatePatternsLoader
    {
        private readonly ResourceHyphenatePatternsLoader originalPatternLoader;
        private readonly string exceptions;
        private readonly string patterns;
        private readonly Language language;

        private static readonly Dictionary<Language, string> patternLookup = new Dictionary<Language, string>();

        public HyphenatePatternsLoader(Language language)
        {
            this.language = language;

            switch (language)
            {
                case Language.enUS:
                    this.originalPatternLoader = new ResourceHyphenatePatternsLoader(HyphenatePatternsLanguage.EnglishUs);
                    return;
                case Language.enGB:
                    this.originalPatternLoader = new ResourceHyphenatePatternsLoader(HyphenatePatternsLanguage.EnglishBritish);
                    return;
                case Language.ruRU:
                    this.originalPatternLoader = new ResourceHyphenatePatternsLoader(HyphenatePatternsLanguage.Russian);
                    return;
                default:
                    break;
            }



            //var names = typeof(HyphenatePatternsLoader).Assembly.GetManifestResourceNames();
            using (var stream = typeof(HyphenatePatternsLoader).Assembly.GetManifestResourceStream($"{typeof(HyphenatePatternsLoader).Assembly.GetName().Name}.Resources.{language}.hyp.txt"))
            using (var reader = new StreamReader(stream))
                this.exceptions = reader.ReadToEnd();

            if (patternLookup.ContainsKey(language))
                this.patterns = patternLookup[language];
            else
            {
                using (var stream = typeof(HyphenatePatternsLoader).Assembly.GetManifestResourceStream($"{typeof(HyphenatePatternsLoader).Assembly.GetName().Name}.Resources.{language}.pat.txt"))
                using (var reader = new StreamReader(stream))
                    this.patterns = reader.ReadToEnd();

                //var patternsClasses = this.patterns
                //    .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                //    .Select(x => new Pattern(x));

                //var orderd = new System.Collections.Generic.SortedList<Pattern, Pattern>();
                //foreach (var item in patternsClasses)
                //    orderd.Add(item, item);

                //this.patterns = string.Join("\r\n", orderd

                //    .Select(x => x.Value.str));


                patternLookup[language] = this.patterns;
            }



        }

        public string LoadExceptions()
        {
            if (this.originalPatternLoader != null)
                return this.originalPatternLoader.LoadExceptions();
            return this.exceptions;
        }

        public string LoadPatterns()
        {
            if (this.originalPatternLoader != null)
                return this.originalPatternLoader.LoadPatterns();
            return this.patterns;

        }



        private sealed class Pattern : IComparer<Pattern>, IComparable<Pattern>
        {
            public readonly string str;
            private readonly int[] levels;

            public int GetLevelByIndex(int index)
            {
                return this.levels[index];
            }

            public int GetLevelsCount()
            {
                return this.levels.Length;
            }

            public Pattern(string str, IEnumerable<int> levels)
            {
                this.str = str;
                this.levels = levels.ToArray();
            }


            public Pattern(string str)
            {
                this.str = str;
                this.levels = new int[0];
            }

            public static int Compare(Pattern x, Pattern y)
            {
                bool first = x.str.Length < y.str.Length;
                int minSize = first ? x.str.Length : y.str.Length;
                for (var i = 0; i < minSize; ++i)
                {
                    if (x.str[i] < y.str[i])
                        return -1;
                    if (x.str[i] > y.str[i])
                        return 1;
                }
                return first ? -1 : 1;
            }

            int IComparer<Pattern>.Compare(Pattern x, Pattern y)
            {
                return Compare(x, y);
            }

            public int CompareTo(Pattern other)
            {
                return Compare(this, other);
            }
        }
    }
}

namespace Stasistium.PDF
{
    enum Language
    {
        enUS,
        enGB,
        ruRU,
        deDE
    }
}