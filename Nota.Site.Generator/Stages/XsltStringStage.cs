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

    internal class DocumentResolver : XmlResolver
    {
        private ImmutableList<IDocument<Stream>> allFiles;
        private IDocument<Stream> relativeTo;

        public DocumentResolver(ImmutableList<IDocument<Stream>> allFiles, IDocument<Stream> relativeTo)
        {
            this.allFiles = allFiles;
            this.relativeTo = relativeTo;
        }

        
        public override Uri ResolveUri(Uri? baseUri, string? relativeUri)
        {
            Uri root = new Uri("file:///", UriKind.Absolute);
            return new Uri(root, relativeUri);
        }

        public override object? GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn)
        {

            if (ofObjectToReturn is not null && !ofObjectToReturn.IsAssignableTo(typeof(System.IO.Stream))) {
                return null;
            }


            var path = absoluteUri.LocalPath;

            if (path.StartsWith("/")||path.StartsWith("\\")) {
                path = path.Substring(1);
            }
            path = path.Replace('\\', '/');

            var target = NotaPath.Combine(NotaPath.GetFolder(relativeTo.Id), path);
            var fond = allFiles.FirstOrDefault(x => target == x.Id);

            if (fond is null) {
                return null;
            }

            var stream = fond.Value;


            var documentResolver = new DocumentResolver(allFiles, fond);
            var xmlReaderSettings = new XmlReaderSettings()
            {
                DtdProcessing = DtdProcessing.Parse,
                XmlResolver = documentResolver
            };


            using (var xmlValue = fond.Value)
            using (var xmlReader = XmlReader.Create(xmlValue, xmlReaderSettings))
            using (var reader = new Mvp.Xml.XInclude.XIncludingReader(xmlReader) { XmlResolver = documentResolver }) {

                var doc = new XmlDocument();
                doc.Load(reader);


                return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(doc.OuterXml));
            }
        }
    }

    public class ResolveXmlStageStream : Stasistium.Stages.StageBase<Stream, Stream, string>
    {
        [Stasistium.StageName("ResolveXml")]
        public ResolveXmlStageStream(IGeneratorContext context, string? name) : base(context, name)
        {
        }

        protected override Task<ImmutableList<IDocument<string>>> Work(ImmutableList<IDocument<Stream>> xmlList, ImmutableList<IDocument<Stream>> allFiles, OptionToken options)
        {
            return Task.FromResult(xmlList.Select(xmlStream =>
               {
                   var documentResolver = new DocumentResolver(allFiles, xmlStream);
                   var xmlReaderSettings = new XmlReaderSettings()
                   {

                       XmlResolver = documentResolver
                   };

                   using (var xmlValue = xmlStream.Value)
                   using (var xmlReader = XmlReader.Create(xmlValue, xmlReaderSettings))
                   using (var reader = new Mvp.Xml.XInclude.XIncludingReader(xmlReader) { XmlResolver = documentResolver }) {
                       var doc = new XmlDocument();
                       doc.Load(reader);

                       return xmlStream.With(doc.OuterXml, xmlStream.Context.GetHashForString(doc.OuterXml));
                   }

               }).ToImmutableList());
        }
    }

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
                using (var reader = XmlReader.Create(strigReader))
                    xslt.Load(reader);


                var builder = new StringBuilder();

                using (var strigReader = new StringReader(dataDocument.Value))
                using (var reader = XmlReader.Create(strigReader))



                using (var stringWriter = new StringWriter(builder))
                using (var writer = XmlWriter.Create(stringWriter)) {
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
                using (var reader = XmlReader.Create(xsltStream))
                    xslt.Load(reader);


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

