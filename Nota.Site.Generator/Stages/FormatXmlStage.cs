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
                this.Context.Logger.Info($"Faild to parse Xml for {input.Id}");
                return Task.FromResult(input);
            }
        }
    }
}
