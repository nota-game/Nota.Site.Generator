using Nota.Site.Generator.Stages;
using Stasistium.Documents;
using Stasistium.Stages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Xsl;

namespace Nota.Site.Generator.Stages
{
    public class XsltStageString<T1, T2> : Stasistium.Stages.GeneratedHelper.Single.Simple.OutputSingleInputSingleSimple2List0StageBase<string, T1, string, T2, string>
        where T1 : class
        where T2 : class
    {
        public XsltStageString(StageBase<string, T1> inputSingle0, StageBase<string, T2> inputSingle1, IGeneratorContext context, string? name = null) : base(inputSingle0, inputSingle1, context, name, false)
        {
        }


        protected override Task<IDocument<string>> Work(IDocument<string> xsltString, IDocument<string> inputString, OptionToken options)
        {
            var xslt = new XslCompiledTransform();
            using (var strigReader = new StringReader(xsltString.Value))
            using (var xmlReader = XmlReader.Create(strigReader))
                xslt.Load(xmlReader);


            var builder = new StringBuilder();

            using (var strigReader = new StringReader(inputString.Value))
            using (var reader = XmlReader.Create(strigReader))

            using (var stringWriter = new StringWriter(builder))
            using (var writer = XmlWriter.Create(stringWriter))
            {
                xslt.Transform(reader, writer);
            }

            var resultString = builder.ToString();
            return Task.FromResult(inputString.With(resultString, inputString.Context.GetHashForString(resultString)));
        }

    }


    public class XsltStageStream<T1, T2> : Stasistium.Stages.GeneratedHelper.Single.Simple.OutputSingleInputSingleSimple2List0StageBase<Stream, T1, Stream, T2, string>
        where T1 : class
        where T2 : class
    {
        public XsltStageStream(StageBase<Stream, T1> inputSingle0, StageBase<Stream, T2> inputSingle1, IGeneratorContext context, string? name = null) : base(inputSingle0, inputSingle1, context, name, false)
        {
        }


        protected override Task<IDocument<string>> Work(IDocument<Stream> xsltInput, IDocument<Stream> xmlInput, OptionToken options)
        {
            var xslt = new XslCompiledTransform();
            using (var xsltStream = xsltInput.Value)
            using (var xmlReader = XmlReader.Create(xsltStream))
                xslt.Load(xmlReader);

            var txt = new StreamReader(xmlInput.Value);

            var sample = txt.ReadToEnd();

            var builder = new StringBuilder();

            using (var xmlValue = xmlInput.Value)
            using (var reader = XmlReader.Create(xmlValue))
            using (var stringWriter = new StringWriter(builder))
            //using (var textWriter = new TextWriter(stringWriter))
            {
                xslt.Transform(reader, new XsltArgumentList() {  }, stringWriter);
            }

            var resultString = builder.ToString();
            return Task.FromResult(xsltInput.With(resultString, xsltInput.Context.GetHashForString(resultString)));
        }

    }
}

namespace Nota.Site.Generator
{
    public static partial class StageExtensions
    {
        public static XsltStageString<T1, T2> Xslt<T1, T2>(this StageBase<string, T1> xslt, StageBase<string, T2> data, string? name = null)
            where T1 : class
            where T2 : class
        {
            if (xslt is null)
                throw new ArgumentNullException(nameof(xslt));
            return new XsltStageString<T1, T2>(xslt, data, xslt.Context, name);
        }
        public static XsltStageStream<T1, T2> Xslt<T1, T2>(this StageBase<Stream, T1> xslt, StageBase<Stream, T2> data, string? name = null)
            where T1 : class
            where T2 : class
        {
            if (xslt is null)
                throw new ArgumentNullException(nameof(xslt));
            return new XsltStageStream<T1, T2>(xslt, data, xslt.Context, name);
        }
    }
}