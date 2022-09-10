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

#pragma warning disable CS0618 // Type or member is obsolete
        private readonly ResourceHyphenatePatternsLoader originalPatternLoader;
#pragma warning restore CS0618 // Type or member is obsolete
        private readonly string exceptions;
        private readonly string patterns;
        private readonly Language language;

        private static readonly Dictionary<Language, string> patternLookup = new Dictionary<Language, string>();

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public HyphenatePatternsLoader(Language language)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
            this.language = language;

            switch (language) {
                case Language.enUS:
#pragma warning disable CS0618 // Type or member is obsolete
                    originalPatternLoader = new ResourceHyphenatePatternsLoader(HyphenatePatternsLanguage.EnglishUs);
                    return;
                case Language.enGB:
                    originalPatternLoader = new ResourceHyphenatePatternsLoader(HyphenatePatternsLanguage.EnglishBritish);
                    return;
                case Language.ruRU:
                    originalPatternLoader = new ResourceHyphenatePatternsLoader(HyphenatePatternsLanguage.Russian);
#pragma warning restore CS0618 // Type or member is obsolete
                    return;
                default:
                    break;
            }



            //var names = typeof(HyphenatePatternsLoader).Assembly.GetManifestResourceNames();
            using (Stream stream = typeof(HyphenatePatternsLoader).Assembly.GetManifestResourceStream($"{typeof(HyphenatePatternsLoader).Assembly.GetName().Name}.Resources.{language}.hyp.txt")!)
            using (var reader = new StreamReader(stream)) {
                exceptions = reader.ReadToEnd();
            }

            if (patternLookup.ContainsKey(language)) {
                patterns = patternLookup[language];
            } else {
                using (Stream stream = typeof(HyphenatePatternsLoader).Assembly.GetManifestResourceStream($"{typeof(HyphenatePatternsLoader).Assembly.GetName().Name}.Resources.{language}.pat.txt")!)
                using (var reader = new StreamReader(stream)) {
                    patterns = reader.ReadToEnd();
                }

                //var patternsClasses = this.patterns
                //    .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                //    .Select(x => new Pattern(x));

                //var orderd = new System.Collections.Generic.SortedList<Pattern, Pattern>();
                //foreach (var item in patternsClasses)
                //    orderd.Add(item, item);

                //this.patterns = string.Join("\r\n", orderd

                //    .Select(x => x.Value.str));


                patternLookup[language] = patterns;
            }



        }

        public string LoadExceptions()
        {
            if (originalPatternLoader != null) {
                return originalPatternLoader.LoadExceptions();
            }

            return exceptions;
        }

        public string LoadPatterns()
        {
            if (originalPatternLoader != null) {
                return originalPatternLoader.LoadPatterns();
            }

            return patterns;

        }



        private sealed class Pattern : IComparer<Pattern>, IComparable<Pattern>
        {
            public readonly string str;
            private readonly int[] levels;

            public int GetLevelByIndex(int index)
            {
                return levels[index];
            }

            public int GetLevelsCount()
            {
                return levels.Length;
            }

            public Pattern(string str, IEnumerable<int> levels)
            {
                this.str = str;
                this.levels = levels.ToArray();
            }


            public Pattern(string str)
            {
                this.str = str;
                levels = new int[0];
            }

            public static int Compare(Pattern? x, Pattern? y)
            {
                if (x == null && y == null) {
                    return 0;
                }

                if (x == null) {
                    return -1;
                }
                if (y == null) {
                    return 1;
                }
                bool first = x.str.Length < y.str.Length;
                int minSize = first ? x.str.Length : y.str.Length;
                for (int i = 0; i < minSize; ++i) {
                    if (x.str[i] < y.str[i]) {
                        return -1;
                    }

                    if (x.str[i] > y.str[i]) {
                        return 1;
                    }
                }
                return first ? -1 : 1;
            }

            int IComparer<Pattern>.Compare(Pattern? x, Pattern? y)
            {
                return Compare(x, y);
            }

            public int CompareTo(Pattern? other)
            {
                return Compare(this, other);
            }
        }
    }
}

namespace Stasistium.PDF
{
    internal enum Language
    {
        enUS,
        enGB,
        ruRU,
        deDE
    }
}