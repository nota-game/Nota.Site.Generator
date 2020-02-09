using Microsoft.Toolkit.Parsers.Markdown;
using Microsoft.Toolkit.Parsers.Markdown.Blocks;
using Microsoft.Toolkit.Parsers.Markdown.Helpers;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Microsoft.Toolkit.Parsers.Markdown.Inlines;
using Stasistium.Documents;
using System.Threading.Tasks;
using System.Collections.Immutable;
using Stasistium.Stages;
using Stasistium;
using Nota.Site.Generator.Markdown.Blocks;

namespace Nota.Site.Generator.Markdown.Blocks
{
    public class InsertBlock : MarkdownBlock
    {
        public string Reference { get; }

        private InsertBlock(string reference)
        {
            this.Reference = reference;
        }


        public new class Parser : Parser<InsertBlock>
        {
            protected override BlockParseResult<InsertBlock>? ParseInternal(string markdown, int startOfLine, int firstNonSpace, int endOfFirstLine, int maxStart, int maxEnd, bool lineStartsNewParagraph, MarkdownDocument document)
            {
                if (firstNonSpace + 2 > maxEnd
                    || markdown[firstNonSpace] != '~'
                    || markdown[firstNonSpace + 1] != '['
                    || !lineStartsNewParagraph)
                {
                    return null;
                }

                var index = markdown.IndexOf("]", firstNonSpace, endOfFirstLine - firstNonSpace, StringComparison.InvariantCulture);
                if (index == -1)
                    return null;

                foreach (var c in markdown.AsSpan(index + 1, endOfFirstLine - index - 1))
                    if (!char.IsWhiteSpace(c))
                        return null;

                return BlockParseResult.Create(new InsertBlock(markdown.Substring(firstNonSpace + 2, index - firstNonSpace - 2)), firstNonSpace, index + 1);
            }
        }

        public override string ToString()
        {
            return $"~[{this.Reference}]";
        }

    }
    public class InserBlockResolver
    {
        private readonly string relativeTo;
        private readonly Dictionary<string, IDocument<MarkdownDocument>> lookup;

        public InserBlockResolver(IDocument relativeTo, IEnumerable<IDocument<MarkdownDocument>> documents, IGeneratorContext context)
        {
            this.relativeTo = relativeTo.Id;
            this.lookup = documents.SelectMany(this.GetPathes).ToDictionary(x => x.id, x => x.document);
            this.Context = context;
        }

        private IEnumerable<(string id, IDocument<MarkdownDocument> document)> GetPathes(IDocument<MarkdownDocument> document)
        {
            yield return ("/" + document.Id, document);

            var currentFolder = System.IO.Path.GetDirectoryName(this.relativeTo)?.Replace('\\', '/');
            if (currentFolder is null)
                yield break;
            if (!currentFolder.EndsWith('/'))
                currentFolder += '/';
            if (document.Id.StartsWith(currentFolder))
                yield return (document.Id.Substring(currentFolder.Length), document);
        }

        public IGeneratorContext Context { get; }

        public MarkdownDocument Resolve(MarkdownDocument document)
        {
            var newDocument = document.GetBuilder().Build();
            newDocument.Blocks = this.DeepCopy(document.Blocks);
            return newDocument;
        }

        private IList<MarkdownBlock> DeepCopy(IEnumerable<MarkdownBlock> blocks)
        {
            return blocks.Select(this.DeepCopy).ToArray();

        }
        private MarkdownBlock DeepCopy(MarkdownBlock block)
        {
            switch (block)
            {
                case TableBlock tableBlock:
                    {
                        return new TableBlock
                        {
                            ColumnDefinitions = tableBlock.ColumnDefinitions
                                .Select<TableBlock.TableColumnDefinition, TableBlock.TableColumnDefinition>(x => new TableBlock.TableColumnDefinition()
                                {
                                    Alignment = x.Alignment
                                }).ToArray(),
                            Rows = tableBlock.Rows
                                .Select<TableBlock.TableRow, TableBlock.TableRow>(x => new TableBlock.TableRow()
                                {
                                    Cells = x.Cells
                                         .Select<TableBlock.TableCell, TableBlock.TableCell>(y => new TableBlock.TableCell()
                                         {
                                             Inlines = this.DeepCopy(y.Inlines)
                                         }).ToArray()
                                }).ToArray()
                        };
                    }

                case ParagraphBlock paragraphBlock:
                    {

                        return new ParagraphBlock()
                        {
                            Inlines = this.DeepCopy(paragraphBlock.Inlines),
                        };
                    }

                case ListBlock listBlock:
                    {
                        return new ListBlock()
                        {
                            Style = listBlock.Style,
                            Items = listBlock.Items.Select<ListItemBlock, ListItemBlock>(x => new ListItemBlock() { Blocks = this.DeepCopy(x.Blocks) }).ToArray(),
                        };
                    }

                case HeaderBlock headerBlock:
                    {
                        return new HeaderBlock()
                        {
                            HeaderLevel = headerBlock.HeaderLevel,
                            Inlines = this.DeepCopy(headerBlock.Inlines)
                        };
                    }

                case InsertBlock insertBlock:
                    {
                        if (!this.lookup.TryGetValue(insertBlock.Reference, out var referencedDocument))
                            throw this.Context.Exception($"Could not find referenced document {insertBlock.Reference}");

                        return new SoureReferenceBlock(this.DeepCopy(referencedDocument.Value.Blocks), referencedDocument);
                    }

                case SoureReferenceBlock soureReferenceBlock:
                    {
                        return new SoureReferenceBlock(this.DeepCopy(soureReferenceBlock.Blocks), soureReferenceBlock.OriginalDocument);
                    }

                case SideNote b:
                    {
                        return new SideNote(b.Id, b.SideNoteType, b.Distributions, this.DeepCopy(b.Blocks));
                    }

                default:
                    throw new NotSupportedException($"Block of type {block.GetType()} is not supported");
            }
        }

        private MarkdownInline DeepCopy(MarkdownInline inline)
        {
            switch (inline)
            {
                case BoldTextInline boldTextInline:
                    return new BoldTextInline()
                    {
                        Inlines = this.DeepCopy(boldTextInline.Inlines)
                    };

                case ItalicTextInline italicTextInline:
                    return new ItalicTextInline() { Inlines = this.DeepCopy(italicTextInline.Inlines) };

                case EmojiInline emojiInline:
                    return new EmojiInline() { Text = emojiInline.Text };

                case ImageInline imageInline:
                    return new ImageInline()
                    {
                        ReferenceId = imageInline.ReferenceId,
                        RenderUrl = imageInline.RenderUrl,
                        Text = imageInline.Text,
                        Tooltip = imageInline.Tooltip,
                        Url = imageInline.Url
                    };

                case MarkdownLinkInline markdownLinkInline:
                    return new MarkdownLinkInline()
                    {
                        Inlines = this.DeepCopy(markdownLinkInline.Inlines),
                        Url = markdownLinkInline.Url,
                        Tooltip = markdownLinkInline.Tooltip,
                        ReferenceId = markdownLinkInline.ReferenceId
                    };

                case StrikethroughTextInline strikethroughTextInline:
                    {
                        return new StrikethroughTextInline() { Inlines = this.DeepCopy(strikethroughTextInline.Inlines) };
                    }

                case LinkAnchorInline linkAnchorInline:
                    return new LinkAnchorInline()
                    {
                        Link = linkAnchorInline.Link,
                        Raw = linkAnchorInline.Raw,
                    };

                case TextRunInline textRunInline:
                    {
                        return new TextRunInline() { Text = textRunInline.Text };
                    }

                default:
                    throw new NotSupportedException($"Block of type {inline.GetType()} is not supported");
            }
        }
        private IList<MarkdownInline> DeepCopy(IList<MarkdownInline> inlines)
        {

            return inlines.Select(this.DeepCopy).ToArray();
        }

    }


    public class InsertMarkdownStage<TSingleCache, TListItemCache, TListCache> : Stasistium.Stages.GeneratedHelper.Single.Simple.OutputSingleInputSingleSimple1List1StageBase<MarkdownDocument, TSingleCache, MarkdownDocument, TListItemCache, TListCache, MarkdownDocument>
        where TSingleCache : class
        where TListItemCache : class
        where TListCache : class
    {
        public InsertMarkdownStage(StageBase<MarkdownDocument, TSingleCache> inputSingle0, MultiStageBase<MarkdownDocument, TListItemCache, TListCache> inputList0, IGeneratorContext context, string? name) : base(inputSingle0, inputList0, context, name)
        {
        }

        protected override Task<IDocument<MarkdownDocument>> Work(IDocument<MarkdownDocument> inputSingle0, ImmutableList<IDocument<MarkdownDocument>> inputList0, OptionToken options)
        {
            var resolver = new InserBlockResolver(inputSingle0, inputList0, this.Context);

            var resolvedDocument = resolver.Resolve(inputSingle0.Value);

            return Task.FromResult(inputSingle0.With(resolvedDocument, this.Context.GetHashForString(resolvedDocument.ToString())));
        }
    }


}
namespace Nota.Site.Generator
{
    public static partial class StageExtensions
    {
        public static InsertMarkdownStage<TSingleCache, TListItemCache, TListCache> InsertMarkdown<TSingleCache, TListItemCache, TListCache>(this StageBase<MarkdownDocument, TSingleCache> input, MultiStageBase<MarkdownDocument, TListItemCache, TListCache> allDocuments, string? name = null)
        where TSingleCache : class
        where TListItemCache : class
        where TListCache : class
        {
            return new InsertMarkdownStage<TSingleCache, TListItemCache, TListCache>(input, allDocuments, input.Context, name);
        }
    }
}