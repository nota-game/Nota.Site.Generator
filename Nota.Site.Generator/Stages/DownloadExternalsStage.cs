﻿using Stasistium;
using Stasistium.Documents;
using Stasistium.Stages;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Xml;
using Nota.Site.Generator.Stages;
using System.Text;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Text.Unicode;
using AngleSharp;
using AngleSharp.Dom;
using System.Net;
using System.Net.Http;
using System.Diagnostics.CodeAnalysis;

namespace Nota.Site.Generator.Stages
{
    public class DownloadExternalsStage : StageBase<Stream, Stream>
    {
        public DownloadExternalsStage(IGeneratorContext context, string? name) : base(context, name)
        {
        }

        protected override async Task<ImmutableList<IDocument<Stream>>> Work(ImmutableList<IDocument<Stream>> input, OptionToken options)
        {
            var pathLookup = new Dictionary<string, string>();
            var list = await input.ToAsyncEnumerable().SelectMany(x => Work2(x, pathLookup)).ToArrayAsync();
            return list.ToImmutableList();

        }

        private IAsyncEnumerable<IDocument<Stream>> Work2(IDocument<Stream> input, Dictionary<string, string> pathlookup)
        {
            try {
                return Work(input, pathlookup);
            } catch (Exception) {
                this.Context.Logger.Info($"Faild to parse Xml for {input.Id}");
                return Enumerable.Repeat(input, 1).ToAsyncEnumerable();
            }
        }
        private async IAsyncEnumerable<IDocument<Stream>> Work(IDocument<Stream> input, Dictionary<string, string> pathlookup)
        {
            using var client = new HttpClient();
            //Use the default configuration for AngleSharp
            var config = Configuration.Default;

            //Create a new context for evaluating webpages with the given config
            using var context = BrowsingContext.New(config);

            //Just get the DOM representation
            using var document = await context.OpenAsync(req => req.Content(input.Value));

            bool changed = false;

            async Task<(string? id, IDocument<Stream>? document)> TryDownloadFile(string url)
            {
                if (url.StartsWith("http://localhost")) {
                    return (null, null);
                }

                if (!url.StartsWith("http")) {
                    return (url, null);
                }
                try {
                    IDocument<Stream>? resultDocument = null;
                    if (!pathlookup.TryGetValue(url, out var id)) {
                        var data = await client.GetByteArrayAsync(url);
                        var mem = new MemoryStream(data);
                        var hash = this.Context.GetHashForStream(mem);
                        var extension = NotaPath.GetExtension(url);
                        id = hash + extension;

                        pathlookup.Add(url, id);
                        resultDocument = this.Context.CreateDocument(null as Stream, hash, id).With(() => new MemoryStream(data), hash);
                    }

                    return (id, resultDocument);

                } catch (System.Exception e) {
                    this.Context.Logger.Error($"Faild to download {url}: {e}");
                    return (url, null);
                }
            }

            foreach (var item in (document.Head?.ChildNodes as IEnumerable<INode>) ?? Array.Empty<INode>()) {

                if (item is AngleSharp.Html.Dom.IHtmlLinkElement link) {
                    if (!string.IsNullOrWhiteSpace(link.Href)) {
                        changed = true;
                        var (newUrl, resultDocument) = await TryDownloadFile(link.Href);
                        if (resultDocument is not null) {
                            yield return resultDocument;
                        }
                        if (newUrl is not null) {
                            link.Href = "/" + newUrl;
                        }
                    }
                } else if (item is AngleSharp.Html.Dom.IHtmlScriptElement script) {
                    if (!string.IsNullOrWhiteSpace(script.Source) && !script.Source.StartsWith("http://localhost")) {
                        changed = true;
                        var (newUrl, resultDocument) = await TryDownloadFile(script.Source);
                        if (resultDocument is not null) {
                            yield return resultDocument;
                        }
                        if (newUrl is not null) {
                            script.Source = "/" + newUrl;
                        }

                    }
                }
            }
            if (changed) {
                var output = document.DocumentElement.OuterHtml;
                byte[] buffer = Encoding.UTF8.GetBytes(output);

                yield return input.With(() => new MemoryStream(buffer), this.Context.GetHashForString(output));

            } else {
                yield return input;
            }



        }
    }

}
