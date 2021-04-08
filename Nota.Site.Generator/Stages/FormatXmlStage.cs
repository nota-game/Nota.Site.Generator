using Stasistium;
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

namespace Nota.Site.Generator.Stages
{
    public class FormatXmlStage : Stasistium.Stages.StageBaseSimple<string, string>
    {
        public FormatXmlStage(IGeneratorContext context, string? name) : base(context, name)
        {
        }

        protected override Task<IDocument<string>> Work(IDocument<string> input, OptionToken options)
        {
            try
            {
                var doc = new XmlDocument().CreateDocumentFragment();

                doc.InnerXml = input.Value;




                using var textWriter = new StringWriter();
                using (var xmlWriter = new XmlTextWriter(textWriter) { Formatting = Formatting.Indented })
                {
                    doc.WriteTo(xmlWriter);

                }

                var result = textWriter.ToString();


                return Task.FromResult(input.With(result, this.Context.GetHashForString(result)));
            }
            catch (Exception)
            {
                this.Context.Logger.Info("Faild to parse Xml");
                return Task.FromResult(input);
            }
        }
    }
    public class DebugStage<T> : Stasistium.Stages.StageBase<T, T>
    {
        private readonly string tag;

        public DebugStage(IGeneratorContext context, string tag, string? name) : base(context, name)
        {
            this.tag = tag;
        }

        protected override  Task<ImmutableList<IDocument<T>>> Work(ImmutableList<IDocument<T>> input, OptionToken options)
        {
            if (input is ImmutableList<IDocument<Stream>> streamList)
            {
                var list = new List<(string id, string content)>();
                foreach (var item in streamList)
                {
                    try
                    {
                        using var stream = item.Value;
                        using var buf = new StreamReader(stream);
                        var text = buf.ReadToEnd();
                        list.Add((item.Id, text));

                    }
                    catch (Exception e)
                    {
                        list.Add((item.Id, e.ToString()));

                    }

                }
                list.ToString();
            }

            System.Diagnostics.Debug.WriteLine($"{this.tag}\t{Print(options)}");
            //return input;
            return Task.FromResult(input);
        }

        private static string Print(OptionToken options)
        {
            return string.Join(", ", options.GenerationId);
        }
    }
}
