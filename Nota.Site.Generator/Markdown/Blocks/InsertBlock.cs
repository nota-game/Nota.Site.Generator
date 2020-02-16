using Microsoft.Toolkit.Parsers.Markdown;
using Microsoft.Toolkit.Parsers.Markdown.Blocks;
using Microsoft.Toolkit.Parsers.Markdown.Helpers;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Microsoft.Toolkit.Parsers.Markdown.Inlines;
using Stasistium.Documents;
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
        private readonly Dictionary<string, IDocument<MarkdownDocument>> lookup;
        private readonly RelativePathResolver resolver;

        public InserBlockResolver(RelativePathResolver resolver, IEnumerable<IDocument<MarkdownDocument>> documents, IGeneratorContext context)
        {
            this.lookup = documents.ToDictionary(x => x.Id);
            this.resolver = resolver;
            this.Context = context;
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
                        var id = this.resolver[insertBlock.Reference];
                        if (id == null || !this.lookup.TryGetValue(id, out var referencedDocument))
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


}


