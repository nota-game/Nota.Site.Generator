using AdaptMark.Markdown.Blocks;
using AdaptMark.Parsers.Markdown;
using AdaptMark.Parsers.Markdown.Blocks;
using AdaptMark.Parsers.Markdown.Inlines;

using Nota.Site.Generator.Markdown.Blocks;

using Stasistium.Documents;
using Stasistium.Stages;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

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

                Stack<HeaderBlock> headers = new Stack<HeaderBlock>();
                List<ImageReference> list = new List<ImageReference>();

                MarkdownDocument doc = docDocument.Value;
                string currentDocumentId = docDocument.Id;

                SearchBlocks(doc.Blocks);

                void SearchBlocks(IEnumerable<MarkdownBlock> blocks)
                {
                    foreach (MarkdownBlock block in blocks) {
                        if (block is HeaderBlock header) {
                            while (headers.Count > 1 && headers.Peek().HeaderLevel >= header.HeaderLevel) {
                                _ = headers.Pop();
                            }

                            headers.Push(header);
                        }

                        if (block is IBlockContainer container) {
                            SearchBlocks(container.Blocks);
                        }
                        if (block is IInlineContainer inlines) {
                            SearchInlines(inlines.Inlines);
                        }

                    }
                }
                void SearchInlines(IEnumerable<MarkdownInline> inlines)
                {
                    foreach (MarkdownInline inline in inlines) {
                        switch (inline) {
                            case ImageInline image: {
                                    string headerString;
                                    if (headers.Count > 0) {
                                        if (headers.Peek() is ChapterHeaderBlock chapterHeaderBlock && chapterHeaderBlock.ChapterId is not null) {
                                            headerString = chapterHeaderBlock.ChapterId;
                                        } else {
                                            headerString = StichStage.GenerateHeaderString(headers);
                                        }
                                    } else {
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
                            case IInlineContainer container: {
                                    SearchInlines(container.Inlines);
                                    break;
                                }
                            default:
                                break;
                        }
                    }
                }

                ImageReferences data = new ImageReferences() { References = list.OrderBy(x => x.Header).ThenBy(x => x.ReferencedId).ToArray() };

                IDocument<MarkdownDocument> document = docDocument.With(docDocument.Metadata.Add(data));


                return document;
            }).ToImmutableList());

        }
    }



    public class ImageReference
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public string ReferencedId { get; set; }
        public string Document { get; set; }
        public BookVersion Version { get; set; }
        public BookMetadata Book { get; set; }
        public string Header { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

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
        public ImageReference[] References { get; set; } = Array.Empty<ImageReference>();
    }
}
