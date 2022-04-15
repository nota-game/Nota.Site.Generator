using AdaptMark.Parsers.Markdown;
using Stasistium.Documents;
using System.Threading.Tasks;
using System.Collections.Immutable;
using Stasistium.Stages;
using Nota.Site.Generator.Stages;
using Nota.Site.Generator.Markdown.Blocks;
using System.Collections.Generic;
using System;
using System.Linq;
using AdaptMark.Parsers.Markdown.Blocks;
using AdaptMark.Parsers.Markdown.Inlines;
using Microsoft.Net.Http.Headers;
using AdaptMark.Markdown.Blocks;

namespace Nota.Site.Generator.Stages
{

    public class GetReferenceLocationStage : StageBase<MarkdownDocument, MarkdownDocument>
    {




        public GetReferenceLocationStage(IGeneratorContext context, string? name = null) : base(context, name)
        {
            
        }

        protected override Task<ImmutableList<IDocument<MarkdownDocument>>> Work(ImmutableList<IDocument<MarkdownDocument>> input, OptionToken options)
        {
            return Task.FromResult(input.Select(docDocument =>
            {

                var headers = new Stack<HeaderBlock>();
                var list = new List<ImageReference>();

                var doc = docDocument.Value;
                var currentDocumentId = docDocument.Id;

                SearchBlocks(doc.Blocks);

                void SearchBlocks(IEnumerable<MarkdownBlock> blocks)
                {
                    foreach (var block in blocks)
                    {
                        if (block is HeaderBlock header)
                        {
                            while (headers.Count > 1 && headers.Peek().HeaderLevel >= header.HeaderLevel)
                                headers.Pop();
                            headers.Push(header);
                        }

                        if (block is IBlockContainer container)
                        {
                            SearchBlocks(container.Blocks);
                        }
                        if (block is IInlineContainer inlines)
                        {
                            SearchInlines(inlines.Inlines);
                        }

                    }
                }
                void SearchInlines(IEnumerable<MarkdownInline> inlines)
                {
                    foreach (var inline in inlines)
                    {
                        switch (inline)
                        {
                            case ImageInline image:
                                {
                                    string headerString;
                                    if (headers.Count > 0)
                                    {
                                        if (headers.Peek() is ChapterHeaderBlock chapterHeaderBlock && chapterHeaderBlock.ChapterId is not null)
                                        {
                                            headerString = chapterHeaderBlock.ChapterId;
                                        }
                                        else
                                        {
                                            headerString = StichStage.GenerateHeaderString(headers);
                                        }
                                    }
                                    else
                                    {
                                        headerString = string.Empty;
                                    }

                                    list.Add(new ImageReference()
                                    {
                                        ReferencedId = image.Url,
                                        Document = currentDocumentId,
                                        Header = headerString
                                    });
                                    break;
                                }
                            case IInlineContainer container:
                                {
                                    SearchInlines(container.Inlines);
                                    break;
                                }
                            default:
                                break;
                        }
                    }
                }

                var data = new ImageReferences() { References = list.OrderBy(x => x.Header).ThenBy(x => x.ReferencedId).ToArray() };

                var document = docDocument.With(docDocument.Metadata.Add(data));


                return document;
            }).ToImmutableList());

        }
    }



    public class ImageReference
    {
        public string ReferencedId { get; set; }
        public string Document { get; set; }
        public BookVersion Version { get; set; }
        public BookMetadata Book { get; set; }
        public string Header { get; set; }

        public override bool Equals(object? obj)
        {
            return obj is ImageReference reference &&
                   ReferencedId == reference.ReferencedId &&
                   Document == reference.Document &&
                   Version.Equals(reference.Version) &&
                   Header == reference.Header;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ReferencedId, Document, Version, Header);
        }
    }

    public class ImageReferences
    {
        public ImageReference[] References { get; set; }
    }
}
