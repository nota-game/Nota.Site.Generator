using MetadataExtractor;
using Stasistium.Documents;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Stasistium.Stages
{
    public class EmbededXmpStage<T> : Stasistium.Stages.GeneratedHelper.Single.Simple.OutputSingleInputSingleSimple1List0StageBase<Stream, T, Stream>
        where T : class
    {
        private static readonly ImmutableDictionary<string, string> namespaceAlias =
            new Dictionary<string, string>
            {
                { "dc", "http://purl.org/dc/elements/1.1/" },
                { "xmpRights", "http://ns.adobe.com/xap/1.0/rights/" },
                { "cc", "http://creativecommons.org/ns#" },
                { "xmp", "http://ns.adobe.com/xap/1.0/" },
                { "xml", "http://www.w3.org/XML/1998/namespace" },
            }.ToImmutableDictionary();

        public EmbededXmpStage(StageBase<Stream, T> inputSingle0, IGeneratorContext context, string? name) : base(inputSingle0, context, name)
        {
        }

        protected override async Task<IDocument<Stream>> Work(IDocument<Stream> input, OptionToken options)
        {

            using (var stream = input.Value)
            {
                var readed = ImageMetadataReader.ReadMetadata(stream).OfType<MetadataExtractor.Formats.Xmp.XmpDirectory>().FirstOrDefault();
                if (readed?.XmpMeta is null)
                    return input;


                var culture = new System.Globalization.CultureInfo("de-de");

                var license = ToSearchLanguage("dc:rights", culture)
                    ?? ToSearchLanguage("xmpRights:UsageTerms", culture)
                    ?? ToSearchLanguage("cc:license", culture);

                var creator = ToSearchArray("dc:creator");
                if (creator is null || creator.Length == 0)
                {
                    var ccCreator = ToSearch("cc:attributionName");
                    if (ccCreator != null)
                        creator = new string[] { ccCreator };
                }
                bool? rightsReserved;
                if (bool.TryParse(ToSearch("xmpRights:Marked"), out var rightsReservedValue))
                    rightsReserved = rightsReservedValue;
                else
                    rightsReserved = null;

                if (license == null && creator == null && rightsReserved == null)
                {
                    return input;
                }

                return input.With(input.Metadata.Add(new XmpMetadata(creator, license, rightsReserved)));


                string ToSearch(string toSearch)
                {
                    var xmpPropertyInfo = readed.XmpMeta.Properties.FirstOrDefault(x =>
                    {

                        var (ns, name) = LocalName(toSearch);
                        return x.Namespace == ns && LocalName(x.Path).localName == name;
                    });
                    if (xmpPropertyInfo is null)
                        return null;

                    if (xmpPropertyInfo.Options.IsArray)
                    {
                        var data = new List<string>();

                        var index = 1;
                        while (true)
                        {

                            var entry = readed.XmpMeta.Properties.FirstOrDefault(x => $"{xmpPropertyInfo.Path}[{index}]" == x.Path)?.Value;

                            if (entry is null)
                                break;

                            data.Add(entry);

                            index++;
                        }

                        if (data.Count == 1)
                            return data.First();

                        if (data.Count == 0)
                            return xmpPropertyInfo.Value;

                        throw new InvalidOperationException($"Multiple elements found for {xmpPropertyInfo.Path}");
                    }

                    return xmpPropertyInfo?.Value;
                }

                string[]? ToSearchArray(string toSearch)
                {
                    var xmpPropertyInfo = readed.XmpMeta.Properties.FirstOrDefault(x =>
                    {

                        var (ns, name) = LocalName(toSearch);
                        return x.Namespace == ns && LocalName(x.Path).localName == name;
                    });
                    if (xmpPropertyInfo is null)
                        return null;

                    if (xmpPropertyInfo.Options.IsArray)
                    {
                        var data = new List<string>();

                        var index = 1;
                        while (true)
                        {

                            var entry = readed.XmpMeta.Properties.FirstOrDefault(x => $"{xmpPropertyInfo.Path}[{index}]" == x.Path)?.Value;

                            if (entry is null)
                                break;

                            data.Add(entry);

                            index++;
                        }


                        if (data.Count > 0)
                            return data.ToArray();

                    }

                    return new string[] { xmpPropertyInfo?.Value };
                }

                string? ToSearchLanguage(string toSearch, System.Globalization.CultureInfo language)
                {
                    var xmpPropertyInfo = readed.XmpMeta.Properties.FirstOrDefault(x =>
                    {

                        var (ns, name) = LocalName(toSearch);
                        return x.Namespace == ns && LocalName(x.Path).localName == name;
                    });
                    if (xmpPropertyInfo is null)
                        return null;

                    if (xmpPropertyInfo.Options.IsArray)
                    {
                        var data = new List<(string value, string? lang)>();

                        var index = 1;
                        while (true)
                        {

                            var entry = readed.XmpMeta.Properties.FirstOrDefault(x => $"{xmpPropertyInfo.Path}[{index}]" == x.Path)?.Value;
                            var lang = readed.XmpMeta.Properties.FirstOrDefault(x => $"{xmpPropertyInfo.Path}[{index}]/xml:lang" == x.Path)?.Value;

                            if (entry is null)
                                break;

                            data.Add((entry, lang));

                            index++;
                        }

                        if (data.Count == 0)
                            return xmpPropertyInfo.Value;

                        var currentClture = language;

                        while (true)
                        {
                            if (currentClture == System.Globalization.CultureInfo.InvariantCulture)
                            {
                                return data.FirstOrDefault(x => x.lang == "x-default").value
                                         ?? data.FirstOrDefault(x => string.IsNullOrEmpty(x.lang)).value;
                            }

                            var result = data.FirstOrDefault(x => x.lang == currentClture.Name).value;
                            if (result != null)
                                return result;

                            currentClture = currentClture.Parent;
                        }


                    }

                    return xmpPropertyInfo?.Value;
                }


                static (string? @namespace, string localName) LocalName(ReadOnlySpan<char> input)
                {
                    var splitposition = input.IndexOf(':');
                    if (splitposition == -1)
                        return (default, input.ToString());
                    var ns = input.Slice(0, splitposition);

                    if (!namespaceAlias.TryGetValue(ns.ToString(), out string? nsResult))
                        nsResult = default;

                    return (nsResult, input.Slice(splitposition + 1).ToString());
                }



            }

        }
    }
    public class XmpMetadata
    {
        public XmpMetadata(string[]? creator, string? license, bool? rightsReserved)
        {
            this.Creators = creator;
            this.License = license;
            this.RightsReserved = rightsReserved;
        }

        public bool? RightsReserved { get; }

        public string? License { get; }

        public string[]? Creators { get; }
    }
}
