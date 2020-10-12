using AdaptMark.Parsers.Markdown;
using AdaptMark.Parsers.Markdown.Blocks;
using AdaptMark.Parsers.Markdown.Helpers;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using AdaptMark.Parsers.Markdown.Inlines;
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
            protected override BlockParseResult<InsertBlock>? ParseInternal(in LineBlock markdown, int startLine, bool lineStartsNewParagraph, MarkdownDocument document)
            {

                var line = markdown[startLine];
                var nonspace = line.IndexOfNonWhiteSpace();
                if (nonspace == -1 || nonspace + 2 >= line.Length || line[nonspace] != '~' || line[nonspace + 1] != '[')
                    return null;

                line = line.Slice(nonspace + 1);

                var findClosing = line.FindClosingBrace();

                if (findClosing == -1)
                    return null;

                var reference = line.Slice(1, findClosing - 1);

                var rest = line.Slice(findClosing + 1);
                if (!rest.IsWhiteSpace())
                    return null;

                return BlockParseResult.Create(new InsertBlock(reference.ToString()), startLine, 1);
            }
        }

        protected override string StringRepresentation()
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
                        return new ExtendedTableBlock
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
                                }).ToArray(),
                            HasHeader = (tableBlock as ExtendedTableBlock)?.HasHeader ?? false
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
                        {
                            return new ParagraphBlock() { Inlines = new[] { new TextRunInline() { Text = $"~[{insertBlock.Reference}] Reference was not found" } } };
                        }
                        else
                        {
                            return new SoureReferenceBlock(this.DeepCopy(referencedDocument.Value.Blocks), referencedDocument);
                        }
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


