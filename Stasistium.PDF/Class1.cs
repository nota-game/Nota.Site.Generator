using AdaptMark.Parsers.Markdown;

using System;
using System.Collections.Generic;
using System.Linq;

using Blocks = AdaptMark.Parsers.Markdown.Blocks;
using Inlines = AdaptMark.Parsers.Markdown.Inlines;

namespace Stasistium.PDF
{
    public class Class1
    {
    }

    public class Renderer
    {
        record struct TextRender(PdfSharp.Drawing.XFont Font, PointD Start, String Text);
        record struct ParagraphRender(IEnumerable<TextRender> Renders, double maximumWidth, int numberOfLines, IEnumerable<BlockHolder> requiredBlocks, double lineHight)
        {
            public double calculatedHeight => numberOfLines * lineHight;
        }
        record struct BlockHolder();

        public void Render(MarkdownDocument document)
        {

            var page = new PdfSharp.Pdf.PdfPage();
            var doc = new PdfSharp.Pdf.PdfDocument()
;
            page.Size = PdfSharp.PageSize.A4;
            using var context = PdfSharp.Drawing.XGraphics.CreateMeasureContext(new PdfSharp.Drawing.XSize(210, 300), PdfSharp.Drawing.XGraphicsUnit.Millimeter, PdfSharp.Drawing.XPageDirection.Downwards);


            RectangleD textArea = new RectangleD();
            IEnumerable<RectangleD> excluded = new List<RectangleD>();

            foreach (var block in document.Blocks)
            {

                double paragraphWidth = textArea.Width;


                int currentLine = 0;
                double currentIndention = 0;

                //double spaceWidth = context.MeasureString(" ", font).Width;

                List<TextRender> renders = new();

                if (block is Blocks.ParagraphBlock paragraph)
                {
                    foreach (var inline in paragraph.Inlines)
                    {

                        static void PrepareText(ReadOnlySpan<char> text, PdfSharp.Drawing.XFont font, PdfSharp.Drawing.XGraphics context, double paragraphWidth, List<TextRender> renders, ref double currentIndention, ref int currentLine)
                        {
                            var splited = text.Split();

                            while (splited.MoveNext())
                            {

                                while (context.MeasureString(splited.FromStartIncludingCurrent, font).Width < paragraphWidth && splited.MoveNext())
                                {
                                    // take more words
                                }
                                if(context.MeasureString(splited.FromStartIncludingCurrent, font).Width < paragraphWidth)
                                {
                                    // we consumed the complete text
                                }
                                else
                                {
                                    // we took more words than fiting in the line
                                    var wordToLong = splited.Current;
                                    splited.MovePrevious(); // so remove the last word
                                    // TODO: hyphnate the word
                                }

                            }
                        }

                        static bool AppndText(ReadOnlySpan<char> text, PdfSharp.Drawing.XFont font, PdfSharp.Drawing.XGraphics context, double paragraphWidth, List<TextRender> renders, ref double currentIndention, ref int currentLine, bool printIfToLong = false)
                        {
                            var measured = context.MeasureString(text, font);

                            if (measured.Width + currentIndention < paragraphWidth || printIfToLong)
                            {
                                renders.Add(new TextRender(font, new PointD(currentIndention, currentLine * font.GetHeight()), text.ToString()));
                                currentIndention += measured.Width;
                                return true;
                            }
                            else if (!text.IsWhiteSpace() && text.Contains(' '))
                            {
                                var splitEnumerator = text.Split();
                                while (splitEnumerator.MoveNext())
                                {
                                    if (!AppndText(splitEnumerator.Current, font, context, paragraphWidth, renders, ref currentIndention, ref currentLine))
                                    {
                                        currentIndention = 0;
                                        currentLine++;
                                        AppndText(splitEnumerator.Current, font, context, paragraphWidth, renders, ref currentIndention, ref currentLine, true);
                                    }
                                    if (!AppndText(" ", font, context, paragraphWidth, renders, ref currentIndention, ref currentLine))
                                    {
                                        currentIndention = 0;
                                        currentLine++;
                                    }
                                }
                                return true;
                            }
                            else
                            {
                                //AppndText(splitEnumerator.Current, font, context, paragraphWidth, renders, ref currentIndention, ref currentLine, true);
                                return false;
                            }

                        }

                        string text;
                        PdfSharp.Drawing.XFont font;
                        if (inline is Inlines.TextRunInline run)
                        {
                            font = new PdfSharp.Drawing.XFont("", 1, PdfSharp.Drawing.XFontStyle.Bold);
                            text = run.Text;
                        }



                        AppndText(text, font, context, paragraphWidth, renders, ref currentIndention, ref currentLine);

                    }
                }
            }

            if (document.Blocks.First() is AdaptMark.Parsers.Markdown.Blocks.ParagraphBlock paragrap)
            {
                AdaptMark.Parsers.Markdown.Inlines.TextRunInline r;
                //paragrap.Inlines.
            }


        }
    }


    public enum PageLayout
    {
        SinglePage,
        DoublePageStartLeft,
        DoublePageStartRight,
        DoublePageStartAny,
    }

    public class PageMaster
    {
        public PageMaster Base { get; }

        public IList<Layer> Layers { get; }


    }

    public class Element
    {

    }

    public class Image : Element
    {

    }

    public class TextBox : Element
    {
        public int Columns { get; }
        public double ColumnSpacing { get; }
        public TextBox Next { get; }
        public TextBox Previous { get; }
    }


    public class ParagraphCollection
    {
        public IList<ParagraphSetting> Headding { get; }
        public ParagraphSetting Paragraph { get; }
        public FontSetting TableText { get; }
        public FontSetting TableHeaderColumn { get; }
        public FontSetting TableHeaderRow { get; }
    }

    public class TableSetting
    {
        public FontSetting TableText { get; }
        public FontSetting TableHeaderColumn { get; }
        public FontSetting TableHeaderRow { get; }
    }

    public enum FontWeight
    {
        Regular,
        Bold
    }

    [Flags]
    public enum FontDecoration
    {
        None,
        Underline = 1,
        Strikethrough = 2,
        Cursiv = 4

    }

    public class FontSetting
    {
        public Font Font { get; }
        public double Size { get; }
        public FontWeight Weight { get; }
        public FontDecoration Decoration { get; }
    }

    public enum TextAlignment
    {
        Justyfy,
        Left,
        Right
    }

    public class ParagraphSetting
    {
        public double LineSpacing { get; }
        public FontSetting Font { get; }

        public double MarginLeft { get; }
        public double MarginRight { get; }
        public TextAlignment TextAlignment { get; }
        public double MarginBefore { get; }
        public double MarginAfter { get; }



    }

    public class Font
    {
        IList<string> FontPriorety { get; }
    }

    public class Layer
    {
        public IList<Element> Elements { get; }
    }


    public class Section
    {

    }

    public class Book
    {

    }
}
