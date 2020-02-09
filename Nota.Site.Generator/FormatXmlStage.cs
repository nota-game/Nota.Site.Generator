using Stasistium;
using Stasistium.Documents;
using Stasistium.Stages;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using Nota.Site.Generator.Stages;

namespace Nota.Site.Generator.Stages
{
    public class FormatXmlStage<T> : Stasistium.Stages.GeneratedHelper.Single.Simple.OutputSingleInputSingleSimple1List0StageBase<string, T, string>
        where T : class
    {
        public FormatXmlStage(StageBase<string, T> inputSingle0, IGeneratorContext context, string? name) : base(inputSingle0, context, name)
        {
        }

        protected override async Task<IDocument<string>> Work(IDocument<string> input, OptionToken options)
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


                return input.With(result, this.Context.GetHashForString(result));
            }
            catch (Exception)
            {
                this.Context.Logger.Info("Faild to parse Xml");
                return input;
            }
        }
    }

}

namespace Nota.Site.Generator
{
    public static partial class StageExtensions
    {
        public static FormatXmlStage<T> FormatXml<T>(this StageBase<string, T> input, string? name = null)
        where T : class
        {
            if (input is null)
                throw new ArgumentNullException(nameof(input));
            return new FormatXmlStage<T>(input, input.Context, name);
        }
    }
}