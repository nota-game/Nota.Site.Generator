using Nota.Site.Generator.Stages;
using Stasistium.Documents;
using Stasistium.Stages;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Xsl;

namespace Nota.Site.Generator.Stages
{
    public class XsltStringStage : Stasistium.Stages.StageBase<string, string, string>
    {
        public XsltStringStage(IGeneratorContext context, string? name = null) : base(context, name)
        {
        }


        protected override Task<ImmutableList<IDocument<string>>> Work(ImmutableList<IDocument<string>> xsltString, ImmutableList<IDocument<string>> dataString, OptionToken options)
        {
            var cross = from xsltDocument in xsltString
                        from dataDocument in dataString
                        select (xslt: xsltDocument, data: dataDocument);
            return Task.FromResult(cross.Select(tupple =>
            {
                var (xsltDocument, dataDocument) = tupple;

                var xslt = new XslCompiledTransform();
                using (var strigReader = new StringReader(xsltDocument.Value))
                using (var xmlReader = XmlReader.Create(strigReader))
                    xslt.Load(xmlReader);


                var builder = new StringBuilder();

                using (var strigReader = new StringReader(dataDocument.Value))
                using (var reader = XmlReader.Create(strigReader))

                using (var stringWriter = new StringWriter(builder))
                using (var writer = XmlWriter.Create(stringWriter))
                {
                    xslt.Transform(reader, writer);
                }

                var resultString = builder.ToString();
                return dataDocument.With(resultString, dataDocument.Context.GetHashForString(resultString));
            }).ToImmutableList());
        }

    }

    public class XsltStageStream : Stasistium.Stages.StageBase<Stream, Stream, string>
    {
        [Stasistium.StageName("Xslt")]
        public XsltStageStream(IGeneratorContext context, string? name = null) : base(context, name)
        {

        }

        protected override Task<ImmutableList<IDocument<string>>> Work(ImmutableList<IDocument<Stream>> xsltStreams, ImmutableList<IDocument<Stream>> dataStreams, OptionToken options)
        {
            var cross = from xsltDocument in xsltStreams
                        from dataDocument in dataStreams
                        select (xslt: xsltDocument, data: dataDocument);
            return Task.FromResult(cross.Select(tupple =>
            {
                var (xsltDocument, dataDocument) = tupple;

                var xslt = new XslCompiledTransform();
                using (var xsltStream = xsltDocument.Value)
                using (var xmlReader = XmlReader.Create(xsltStream))
                    xslt.Load(xmlReader);


                var builder = new StringBuilder();

                using (var xmlValue = dataDocument.Value)
                using (var reader = XmlReader.Create(xmlValue))

                using (var stringWriter = new StringWriter(builder))
                //using (var writer = XmlWriter.Create(stringWriter))
                {
                    xslt.Transform(reader, null, stringWriter);
                }

                var resultString = builder.ToString();
                return xsltDocument.With(resultString, dataDocument.Context.GetHashForString(resultString));
            }).ToImmutableList());
        }



    }
}

