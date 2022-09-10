using AdaptMark.Parsers.Markdown;
using AdaptMark.Parsers.Markdown.Blocks;
using AdaptMark.Parsers.Markdown.Inlines;

using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

using LibGit2Sharp;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

using Nota.Site.Generator.Markdown.Blocks;
using Nota.Site.Generator.Stages;

using Stasistium;
using Stasistium.Documents;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Westwind.AspNetCore.LiveReload;

using Blocks = AdaptMark.Parsers.Markdown.Blocks;
using IDocument = Stasistium.Documents.IDocument;
using Inlines = AdaptMark.Parsers.Markdown.Inlines;

namespace Nota.Site.Generator
{
    public class XmlMetaData
    {
        public XmlMetaData()
        {

        }
        public string? NamespaceVersion { get; set; }
    }

    internal class AllDocumentsThatAreDependedOn
    {
        public string[] DependsOn { get; set; } = Array.Empty<string>();
    }
    public static class Nota
    {
        private static IWebHost? host;

        private static readonly Ganss.XSS.HtmlSanitizer sanitizer = new Ganss.XSS.HtmlSanitizer();

        private static readonly string[] MarkdownExtensions = { ".md", ".xlsx", ".xslt" };
        private static readonly string[] ImageExtensions = { ".png", ".jpeg", ".jpg", ".gif" };

        public static string Sanitize(string html)
        {
            return sanitizer.Sanitize(html);
        }

        public static bool IsMarkdown(string document)
        {
            return MarkdownExtensions.Any(x => Path.GetExtension(document) == x);
        }

        public static bool IsMarkdown(IDocument document)
        {
            return IsMarkdown(document.Id);
        }

        public static bool IsHtml(string document)
        {
            return Path.GetExtension(document) == ".html";
        }

        public static bool IsHtml(IDocument document)
        {
            return IsHtml(document.Id);
        }

        public static bool IsImage(string document)
        {
            return ImageExtensions.Any(x => Path.GetExtension(document) == x);
        }

        public static bool IsImage(IDocument document)
        {
            return IsImage(document.Id);
        }

        private static bool CheckLicense(IDocument<Stream> x)
        {
            if (FileCanHasLicense(x) && x.Metadata.TryGetValue<XmpMetadata>() == null) {
                x.Context.Logger.Error($"Remove {x.Id} because of missing license");
                return false;
            }
            return true;
        }

        public static bool IsMeta(string document)
        {
            return Path.GetExtension(document) == ".meta";
        }

        public static bool IsMeta(IDocument document)
        {
            return IsMeta(document.Id);
        }

        public static bool UseCache = true;

        private static readonly System.Text.RegularExpressions.Regex hostReplacementRegex
                         = new System.Text.RegularExpressions.Regex(@"(?<host>http://nota\.org)/schema/", System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>
        /// The Main Method
        /// </summary>
        /// <param name="configuration">Path to the configuration file (json)</param>
        /// <param name="serve">Set to start dev server</param>
        private static async Task Main(FileInfo? configuration = null, bool serve = false)
        {

            // get version
            string version = (typeof(Nota).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>() is AssemblyInformationalVersionAttribute attribute)
            ? attribute.InformationalVersion
            : "Unknown";



            const string workdirPath = "gitOut";
            const string cache = "cache";
            const string output = "out";


            configuration ??= new FileInfo("config.json");

            if (!configuration.Exists) {
                throw new FileNotFoundException($"No configuration file was found ({configuration.FullName})");
            }

            Config config;
            await using (var context = new GeneratorContext()) {



                Stasistium.Stages.IStageBaseOutput<Config?> configFile = context.StageFromResult("configuration", configuration.FullName, x => x)
                 .File()
                 .Json<Config>("Parse Configuration")
                 ;

                var taskCompletion = new TaskCompletionSource<Config>();
                configFile.PostStages += (ImmutableList<IDocument<Config?>> cache, Stasistium.Stages.OptionToken options) =>
                {
                    Config? value = cache.Single().Value;
                    if (value is not null) {
                        taskCompletion.SetResult(value);
                    } else {
                        taskCompletion.SetException(new IOException("Configuration not found"));
                    }

                    return Task.CompletedTask;
                };

                await context.Run(new GenerationOptions()
                {

                    Refresh = false,
                    CompressCache = true,
                });
                config = await taskCompletion.Task;
            }

            string? editUrl = config.ContentRepo?.Url;
            if (editUrl is not null) {
                if (!editUrl.StartsWith("https://github.com/") || !editUrl.EndsWith(".git")) {
                    editUrl = null;
                } else {
                    editUrl = editUrl[..^".git".Length] + "/edit/";
                }
            }

            // Create the committer's signature and commit
            var author = new LibGit2Sharp.Signature("NotaSiteGenerator", "@NotaSiteGenerator", DateTime.Now);
            Signature committer = author;

            var s = System.Diagnostics.Stopwatch.StartNew();


            using Repository repo = PreGit(config, author, workdirPath, cache, output);
            if (File.Exists(Path.Combine(cache, "sourceVersion"))) {
                string oldVersion = await File.ReadAllTextAsync(Path.Combine(cache, "sourceVersion"));
                if (oldVersion != version || version.EndsWith("-dirty")) {
                    UseCache = false;
                }
            } else {
                // no version to check, ignore cache
                UseCache = false;
            }
            await using (var context = new GeneratorContext()) {
                context.Logger.Info($"Code Version {version}");
                context.Logger.Info($"Using cache: {UseCache}");
                context.CacheFolder.Create();
                await File.WriteAllTextAsync(Path.Combine(context.CacheFolder.FullName, "sourceVersion"), version);

                Stasistium.Stages.IStageBaseOutput<Config?> configFile = context.StageFromResult("configuration", configuration.FullName, x => x)
                 .File()
                 .Json<Config>("Parse Configuration")
                 ;

                if (serve) {
                    var workingDir = new DirectoryInfo(output);
                    workingDir.Create();
                    host = new WebHostBuilder()
                    .UseKestrel()
                    .UseContentRoot(workingDir.FullName)
                    .UseWebRoot(workingDir.FullName)
                    .ConfigureServices(services =>
                    {
                        _ = services.AddLiveReload();
                    })
                    .Configure(app =>
                    {
                        _ = app.UseLiveReload();
                        _ = app.UseStaticFiles();
                    })
                    .Build();
                    await host.StartAsync();

                    Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature? feature = host.ServerFeatures.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>();
                    foreach (string item in feature?.Addresses ?? Array.Empty<string>()) {
                        Console.WriteLine($"Listinging to: {item}");
                    }
                }



                Stasistium.Stages.IStageBaseOutput<GitRefStage> contentRepo = configFile
                    .Select(x => x.With(x.Value?.ContentRepo ?? throw x.Context.Exception($"{nameof(Config.ContentRepo)} not set on configuration."), x.Context.GetHashForObject(x.Value.ContentRepo))
                        .With(x.Metadata.Add(new HostMetadata() { Host = x.Value.Host })))
                    .GitClone("Git for Content")
                    //.Where(x => x.Id == "master") // for debuging 
                    //.Where(x => true) // for debuging 
                    ;

                Stasistium.Stages.IStageBaseOutput<GitRefStage> schemaRepo = configFile
                    .Select(x =>
                        x.With(x.Value?.SchemaRepo ?? throw x.Context.Exception($"{nameof(Config.SchemaRepo)} not set on configuration."), x.Context.GetHashForObject(x.Value.SchemaRepo))
                        .With(x.Metadata.Add(new HostMetadata() { Host = x.Value.Host })))
                    .GitClone("Git for Schema");

                Stasistium.Stages.IStageBaseOutput<Microsoft.Extensions.FileProviders.IFileProvider> layoutProvider = configFile
                    .Select(x => x.With(x.Value?.Layouts ?? "layout", x.Value?.Layouts ?? "layout"), "Transform LayoutProvider from Config")
                    .FileSystem("Layout Filesystem")
                    .FileProvider("Layout", "Layout FIle Provider");

                Stasistium.Stages.IStageBaseOutput<Stream> staticFilesInput = configFile
                    .Select(x => x.With(x.Value?.StaticContent ?? "static", x.Value?.StaticContent ?? "static"), "Static FIle from Config")
                    .FileSystem("static Filesystem");






                var generatorOptions = new GenerationOptions()
                {
                    CompressCache = true,
                    Refresh = true,
                    BreakOnError = System.Diagnostics.Debugger.IsAttached,
                    CheckUniqueID = true
                };




                //   var sassFiles = staticFilesInput
                //.Where(x => Path.GetExtension(x.Id) == ".scss", "Filter .scss")
                //.StreamToText(name: "actual to text for scss");

                Stasistium.Stages.IStageBaseOutput<Stream> staticFiles2 = staticFilesInput
                    //.If
                    .XMP()
                       //;

                       .If(x => Path.GetExtension(x.Id) == ".scss",
                           x =>
                               x.ToText()
                               .Sass()
                               .ToStream()
                           , x => x)

                //.Merge(siteData, (file, y) => file.With(file.Metadata.Add(y.Value)), "Merge SiteData with files")
                ;



                Stasistium.Stages.IStageBaseOutput<Stasistium.Razor.RazorProvider> razorProviderStatic = staticFiles2
                   .FileProvider("Content", "Content file Provider")
                   .Concat(layoutProvider, "Concat Content and layout FileProvider")
                   .RazorProvider("Content", "Layout/ViewStart.cshtml", name: "Razor Provider STATIC with ViewStart");







                Stasistium.Stages.IStageBaseOutput<Stream> comninedFiles = contentRepo
                    .CacheDocument("branchedDocuments", c => c
                     .GroupBy(x => x.Value.FrindlyName,
                     (key, input) => DocumentsForBranch(input, key, editUrl), "Select Content Files from Ref")
                    ).Debug("files")

                    ;



                Stasistium.Stages.IStageBaseOutput<AllBooksMetadata> allBooks = comninedFiles.ListTransform(x =>

                {
                    IEnumerable<BookMetadata> bookData = x.Where(y => y.Metadata.TryGetValue<SiteMetadata>() is not null).SelectMany(y => y.Metadata.GetValue<SiteMetadata>().Books);
                    BookMetadata[] bookArray = bookData.Distinct().ToArray();

                    var books = new AllBooksMetadata()
                    {
                        Books = bookArray
                    };
                    return context.CreateDocument(
                        books, context.GetHashForObject(books), "allBooks"
                        );
                }
                       ); ;

                Stasistium.Stages.IStageBaseOutput<ImageReferences> imageData = comninedFiles.Where(x => x.Metadata.TryGetValue<ImageReferences>() != null)
              .ListTransform(documents =>
              {
                  IOrderedEnumerable<ImageReference> newReferences = documents.SelectMany(x =>
                  {
                      string prefix = NotaPath.Combine("Content", x.Metadata.GetValue<GitRefMetadata>().CalculatedVersion.ToString());
                      return x.Metadata.GetValue<ImageReferences>().References
                          //.Select(y =>
                          //{
                          //    return new ImageReference()
                          //    {
                          //        ReferencedId = NotaPath.Combine(prefix, y.ReferencedId),
                          //        Document = NotaPath.Combine(prefix, y.Document),
                          //        Header = y.Header
                          //    };

                          //})
                          ;
                  })
                  .OrderBy(x => x.Document).ThenBy(x => x.Header).ThenBy(x => x.ReferencedId);


                  var images = new ImageReferences()
                  {
                      References = newReferences.ToArray()
                  };

                  return context.CreateDocument(images, context.GetHashForObject(images), "Images");
              })
              ;


                Stasistium.Stages.IStageBaseOutput<Stream> removedDoubleImages = comninedFiles.Where(x => IsImage(x))
                     .Merge(imageData, (file, image) =>
                     {
                         IEnumerable<ImageReference> references = image.Value.References.Where(x => x.ReferencedId == file.Id);
                         if (references.Any()) {
                             var newData = new ImageReferences()
                             {
                                 References = references.ToArray()
                             };
                             return file.With(file.Metadata.Add(newData));
                         } else {
                             return file;
                         }
                     })
                    .GroupBy(
                    x =>
                    {
                        // Use file hash. Document Hash has also metadata
                        using (Stream stream = x.Value) {
                            return context.GetHashForStream(stream);
                        }
                    },
                    (key, input) =>
                    {
                        Stasistium.Stages.IStageBaseOutput<Stream> erg = input.ListTransform(key, (x, key) =>
                         {
                             if (!x.Skip(1).Any()) {
                                 return x.First();
                             }

                             IDocument<Stream> doc = x.First();
                             doc = doc.WithId(key.Single().Value + NotaPath.GetExtension(doc.Id));
                             foreach (IDocument<Stream> item in x) {
                                 ImageReferences? metadata = item.Metadata.TryGetValue<ImageReferences>();
                                 if (metadata is null) {

                                     metadata = new ImageReferences()
                                     {
                                         References = new[] {new ImageReference() {
                                            ReferencedId = item.Id
                                        } }
                                     };
                                 }
#pragma warning disable CS8621 // Nullability of reference types in return type doesn't match the target delegate (possibly because of nullability attributes).
                                 doc = doc.With(doc.Metadata.AddOrUpdate(metadata, (ImageReferences? oldvalue, ImageReferences newvalue) => new ImageReferences()
                                 {
                                     References = oldvalue?.References.Concat(newvalue.References).Distinct().ToArray() ?? newvalue.References
                                 }));
#pragma warning restore CS8621 // Nullability of reference types in return type doesn't match the target delegate (possibly because of nullability attributes).
                             }

                             // doc.Metadata.GetValue<ImageReferences>().References.Select(y=>
                             // {
                             //     var targetPath = y.ReferencedId;
                             //     relativeTo = y.Document;
                             //     return y;
                             // });

                             return doc;

                         });

                        return erg;
                    })
                    ;


                Stasistium.Stages.IStageBaseOutput<Stream> combinedFiles = comninedFiles
                // .Where(x=>!IsImage(x)) // remove the images since we will get those from removed DoublesImages
                .Merge(removedDoubleImages.ListTransform(x =>
                {
                    //var l = x.Select(y => y).ToArray();
                    return context.CreateDocument(x, context.GetHashForObject(x), "list-of-doubles");
                }), CombineFiles)
                .Where(x => !x.Id.StartsWith("TO_REMOVE"))
                      .Merge(imageData, (file, image) =>
                     {
                         return file.With(file.Metadata.AddOrUpdate(image.Value));

                     })
                   .Concat(removedDoubleImages)
                ;



                Stasistium.Stages.IStageBaseOutput<Stream> files = combinedFiles.Merge(allBooks, (file, allBooks) => file.With(file.Metadata.Add(allBooks.Value)));




                Stasistium.Stages.IStageBaseOutput<LicencedFiles> licesnseFIles = files
                .Concat(staticFiles2)

                 .Where(x => x.Metadata.TryGetValue<XmpMetadata>() != null)
                    .ListTransform(z =>
                    {
                        var metadataFile = new LicencedFiles()
                        {

                            LicenseInfos = z.Select(x =>
                            {
                                return new LicenseInfo
                                {
                                    Id = x.Id,
                                    Metadata = x.Metadata,
                                };
                            }).ToArray()
                        }; ;

                        return context.CreateDocument(metadataFile, context.GetHashForObject(metadataFile), "LicencedFiles");
                    });
                ;





                Stasistium.Stages.IStageBaseOutput<Stream> staticFiles = staticFiles2
                    .Merge(licesnseFIles, (file, value) =>
                    {
                        return file.With(file.Metadata.Add(value.Value));
                    }, "Merge licenses with static files")
                    .Merge(allBooks, (file, value) => file.With(file.Metadata.Add(value.Value)))
                    .If(x => Path.GetExtension(x.Id) == ".cshtml")
                        .Then(x => x

                                .Razor(razorProviderStatic)
                                .ToStream()
                                .Select(doc => doc.WithId(Path.ChangeExtension(doc.Id, ".html"))))
                        .Else(x => x);




                Stasistium.Stages.IStageBaseOutput<Stasistium.Razor.RazorProvider> razorProvider = files
                .Debug("test")
                    .FileProvider("Content", "Content file Provider")
                    .Concat(layoutProvider, "Concat Content and layout FileProvider")
                    .RazorProvider("Content", "Layout/ViewStart.cshtml", name: "Razor Provider with ViewStart")
                    ;

                Stasistium.Stages.IStageBaseOutput<Stream> rendered = files


                        .If(razorProvider
                        , x => Path.GetExtension(x.Id) == ".html")
                            .Then((x, provider) => x
                                    .Debug("before razor")

                                    .Razor(provider)
                                    .ToStream())
                            .Else((x, provider) => x);


                Stasistium.Stages.IStageBaseOutput<Stream> schemaFiles = schemaRepo

                     .Select(x => x.With(x.Metadata.Add(new GitRefMetadata(x.Value.FrindlyName, x.Value.Type))))
                     .Files(true, name: "Schema Repo to Files")
                     .Where(x => System.IO.Path.GetExtension(x.Id) != ".md")
                     .ToText()
                        .Select(y =>
                        {
                            GitRefMetadata gitData = y.Metadata.GetValue<GitRefMetadata>()!;
                            BookVersion version = gitData.CalculatedVersion;
                            string? host = y.Metadata.GetValue<HostMetadata>()!.Host;
                            string newText = hostReplacementRegex.Replace(y.Value, @$"{host?.TrimEnd('/')}/schema/{version}/");
                            return y.With(newText, y.Context.GetHashForString(newText));
                        })
                        .ToStream()
                     .Select(x =>
                     {
                         GitRefMetadata gitData = x.Metadata.GetValue<GitRefMetadata>()!;
                         BookVersion version = gitData.CalculatedVersion;

                         return x.WithId($"schema/{version}/{x.Id.TrimStart('/')}");
                     });

                Stasistium.Stages.IStageBaseOutput<Stream> rendered2 = rendered
                    ;



                rendered2
                   //.Select(x => x.WithId(Path.ChangeExtension(x.Id, ".html")))
                   .Concat(schemaFiles)
                   .Concat(staticFiles)
                    .Where(x => CheckLicense(x))

                   .If(IsHtml).Then(x => x.DownloadExternals())
                   .Else(x => x)
                   //    .DownloadExternals()
                   .Persist(new DirectoryInfo(output))
                   ;

                if (host != null) {

                    s.Stop();
                    context.Logger.Info($"Preperation Took {s.Elapsed}");

                    bool isRunning = true;

                    Console.CancelKeyPress += (sender, e) => isRunning = false;
                    bool forceUpdate = false;
                    while (isRunning) {
                        try {
                            Console.Clear();
                            s.Restart();
                            await context.Run(generatorOptions);
                            //await g.UpdateFiles(forceUpdate).ConfigureAwait(false);
                            s.Stop();
                            context.Logger.Info($"Update Took {s.Elapsed}");
                            Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature? feature = host.ServerFeatures.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>();
                            foreach (string item in feature?.Addresses ?? Array.Empty<string>()) {
                                Console.WriteLine($"Listinging to: {item}");
                            }
                        } catch (Exception e) {

                            Console.WriteLine("Error");
                            Console.Error.WriteLine(e);
                        }

                        Console.WriteLine("Press Q to Quit, any OTHER key to update.");
                        ConsoleKeyInfo key = Console.ReadKey(true);
                        if (key.Key == ConsoleKey.Q) {
                            isRunning = false;
                        }

                        forceUpdate = key.Key == ConsoleKey.U;

                    }
                    Console.WriteLine("Initiate Shutdown");
                    s.Restart();
                    await host.StopAsync();


                } else {
                    await context.Run(generatorOptions);
                    //await g.UpdateFiles().ConfigureAwait(false);
                }

            }


            PostGit(author, committer, repo, config.ContentRepo?.PrimaryBranchName ?? "master", workdirPath, cache, output);
            s.Stop();
            Console.WriteLine($"Finishing took {s.Elapsed}");
        }

        private static IDocument<Stream> CombineFiles(IDocument<Stream> input, IDocument<ImmutableList<IDocument<Stream>>> removed)
        {
            IGeneratorContext context = input.Context;
            IEnumerable<string> removedDocuments = removed.Value.Select(y => y.Metadata.TryGetValue<ImageReferences>())
            .Where(x => x != null)
            .SelectMany(x => x!.References)
            .Select(x => x.ReferencedId);
            if (removedDocuments.Contains(input.Id)) {
                return input.WithId($"TO_REMOVE{input.Id}");
            }

            if (Path.GetExtension(input.Id) == ".html") {
                using Stream stream = input.Value;
                using var reader = new StreamReader(stream);
                string text = reader.ReadToEnd();
                string originalText = text;



                IConfiguration angleSharpConfig = AngleSharp.Configuration.Default;
                IBrowsingContext angleSharpContext = BrowsingContext.New(angleSharpConfig);
                IHtmlParser? parser = angleSharpContext.GetService<IHtmlParser>();
                if (parser is null) {
                    throw new InvalidOperationException("A IHtmlParser should exist.");
                }
                string source = text;
                AngleSharp.Html.Dom.IHtmlDocument document = parser.ParseDocument(source);



                foreach (IDocument<Stream> removedDocument in removed.Value) {
                    ImageReferences? metadata = removedDocument.Metadata.TryGetValue<ImageReferences>();
                    if (metadata is null) {
                        continue;
                    }

                    foreach (ImageReference item in metadata.References) {
                        ReadOnlySpan<char> relativeTo = NotaPath.GetFolder(input.Id).AsSpan();
                        ReadOnlySpan<char> absolute = item.ReferencedId.AsSpan();

                        ReadOnlySpan<char> clipedRelative = GetFirstPart(relativeTo, out ReadOnlySpan<char> relativeToRest);
                        ReadOnlySpan<char> clipedAbsolute = GetFirstPart(absolute, out ReadOnlySpan<char> absolutRest);

                        while (MemoryExtensions.Equals(clipedAbsolute, clipedRelative, StringComparison.Ordinal)) {
                            relativeTo = relativeToRest;
                            absolute = absolutRest;
                            clipedRelative = GetFirstPart(relativeTo, out relativeToRest);
                            clipedAbsolute = GetFirstPart(absolute, out absolutRest);

                        }

                        while (!GetFirstPart(relativeTo, out relativeToRest).IsEmpty) {
                            relativeTo = relativeToRest;
                            absolute = ("../" + absolute.ToString()).AsSpan();
                        }



                        ReadOnlySpan<char> GetFirstPart(ReadOnlySpan<char> t, out ReadOnlySpan<char> rest)
                        {
                            int index = t.IndexOf('/');
                            if (index == -1) {
                                rest = string.Empty;
                                return t;
                            } else {
                                rest = t[(index + 1)..^0];
                                return t[0..index];
                            }
                        }
                        string absoluteStr = absolute.ToString();
                        // document.QuerySelectorAll($"img[src=\"{absolute.ToString()}\"").Attr("src", "/" + removedDocument.Id);
                        // document.QuerySelectorAll($"img[src=\"{item.ReferencedId}\"").Attr("src", "/" + removedDocument.Id);

                        foreach (IElement? ele in document.All.Where(m => m.LocalName == "img" && m.Attributes.Any(x => x.LocalName == "src" && (x.Value == absoluteStr || x.Value == item.ReferencedId)))) {
                            XmpMetadata? licenseData = removedDocument.Metadata.TryGetValue<XmpMetadata>();

                            if (licenseData != null) {
                                if (licenseData.RightsReserved.HasValue) {
                                    ele.SetAttribute("rightsReseved", licenseData.RightsReserved.Value.ToString());
                                }

                                if (licenseData.License != null) {
                                    ele.SetAttribute("license", Sanitize(licenseData.License));
                                }

                                if (licenseData.Creators != null) {
                                    ele.SetAttribute("creators", string.Join(", ", licenseData.Creators));
                                }
                            }

                            ele.SetAttribute("src", "/" + removedDocument.Id);
                        }

                        // document.QuerySelectorAll($"img[src=\"{item.ReferencedId}\"").;

                        // text = text.Replace(absolute.ToString(), "/" + removedDocument.Id);
                        // text = text.Replace(item.ReferencedId, "/" + removedDocument.Id);
                    }
                }
                text = document.DocumentElement.OuterHtml;
                if (originalText != text) {
                    byte[] data = Encoding.UTF8.GetBytes(text);
                    return input.With(() => new MemoryStream(data), context.GetHashForString(text));
                }
            }

            return input;
        }
        private static Stasistium.Stages.IStageBaseOutput<Stream> DocumentsForBranch(Stasistium.Stages.IStageBaseOutput<GitRefStage> input, Stasistium.Stages.IStageBaseOutput<string> key, string? editUrl)
        {
            Stasistium.Stages.IStageBaseOutput<SiteMetadata> siteData;
            Stasistium.Stages.IStageBaseOutput<Stream> grouped;
            //NewMethod(input, context, out siteData, out grouped);
            IGeneratorContext context = input.Context;
            Stasistium.Stages.IStageBaseOutput<Stream> contentFiles = input
           .Select(x => x.With(x.Metadata.Add(new GitRefMetadata(x.Value.FrindlyName, x.Value.Type))), "Add GitMetada (Content)")
           .Files(addGitMetadata: true, name: "Read Files from Git (Content)")
                        .XMP()

           .Sidecar<BookMetadata>(".metadata")
               //.Select(input2 =>
               //    input2
               //    .Select(x =>
               //    {
               //        var getData = x.Metadata.GetValue<GitMetadata>();
               //        var newId = $"{ Ids.GetIdWithoutExtension(x.Id)}.{getData.CalculatedVersion.Name}{Ids.GetExtension(x.Id)}";
               //        return x.WithId(newId);
               //    }, "Transform ID so it has the version")
               //    )

               ;
            //return startData;



            // 


            Stasistium.Stages.IStageBaseOutput<Stream> dataFile = contentFiles
                .Sidecar<XmlMetaData>(".xmlmeta")
                .Where(x => x.Id == "data/nota.xml")
                .Single();


            siteData = contentFiles
                .Where(x => x.Id.EndsWith("/.bookdata"), "only bookdata")
                .Markdown(GenerateMarkdownDocument, "siteData markdown")
                    .YamlMarkdownToDocumentMetadata<BookMetadata>("sitedata YamlMarkdown")

                    .Select(x =>
                    {
                        int startIndex = x.Id.IndexOf('/') + 1;
                        int endIndex = x.Id.IndexOf('/', startIndex);
                        string key = x.Id[startIndex..endIndex];

                        string location = NotaPath.Combine("Content", x.Metadata.GetValue<GitRefMetadata>()!.CalculatedVersion.ToString(), key);

                        return ArgumentBookMetadata(x, location)
                            .WithId(location);
                    })
                .Where(x => x.Metadata.TryGetValue<BookMetadata>() != null, "filter book in sitedata without Bookdata")
                .ListTransform(input =>
                {
                    var siteMetadata = new SiteMetadata()
                    {
                        Books = input.Select(x => x.Metadata.GetValue<BookMetadata>()).ToArray()
                    };
                    return context.CreateDocument(siteMetadata, context.GetHashForObject(siteMetadata), "siteMetadata");
                }, "make siteData to single element");







            Stasistium.Stages.IStageBaseOutput<Stream> books = contentFiles.Where(x => x.Id.StartsWith("books/"));
            grouped = books




                .GroupBy
                (dataFile, x =>
                 {
                     int startIndex = x.Id.IndexOf('/') + 1;
                     int endIndex = x.Id.IndexOf('/', startIndex);
                     return x.Id[startIndex..endIndex];
                 }

            , (key, x, dataFile) => GetBooksDocuments(key, x, dataFile, editUrl), "Group by for files");


            Stasistium.Stages.IStageBaseOutput<Stream> files = grouped
                .Select(x => x.With(x.Metadata.Add(new PageLayoutMetadata() { Layout = "book.cshtml" })))
                .Merge(siteData, (file, y) => file.With(file.Metadata.Add(y.Value)), "Merge SiteData with files")
                .Concat(dataFile.Select(x =>
                {
                    XmlMetaData? xmlData = x.Metadata.TryGetValue<XmlMetaData>();
                    string version;
                    if (xmlData is null) {
                        System.Console.WriteLine("Did not fond .xmlmeta");
                        version = "";
                    } else {
                        version = xmlData.NamespaceVersion ?? string.Empty;
                    }



                    string? host = x.Metadata.GetValue<HostMetadata>()!.Host;
                    string newText = hostReplacementRegex.Replace(x.Value.ReadString(), @$"{host?.TrimEnd('/')}/schema/{version}/");
                    string location = NotaPath.Combine("Content", x.Metadata.GetValue<GitRefMetadata>()!.CalculatedVersion.ToString(), x.Id);
                    return x.WithId(location).With(() => newText.ToStream(), x.Context.GetHashForString(newText));
                }))
                //.Merge(siteData, (file, y) => file.With(file.Metadata.Add(y.Value)), "Merge SiteData with files")

                ;


            return files;
        }

        //private static Stasistium.Stages.IStageBaseOutput<Stream> GetBooksDocuments(Stasistium.Stages.IStageBaseOutput<Stream> inputOriginal, string key)
        private static Stasistium.Stages.IStageBaseOutput<Stream> GetBooksDocuments(Stasistium.Stages.IStageBaseOutput<string> key, Stasistium.Stages.IStageBaseOutput<Stream> inputOriginal, Stasistium.Stages.IStageBaseOutput<Stream> dataFile, string? editUrl)
        {
            IGeneratorContext context = inputOriginal.Context;




            Stasistium.Stages.IStageBaseOutput<Stream> input = inputOriginal.CrossJoin(key, (x, key) => x.WithId(x.Id[$"books/{key.Value}/".Length..]).With(x.Metadata.Add(new Book(key.Value))), "input chnage cross join");

            Stasistium.Stages.IStageBaseOutput<MarkdownDocument> bookData = input.Where(x => x.Id == $".bookdata")
                .Single()
                .Markdown(GenerateMarkdownDocument)
                .YamlMarkdownToDocumentMetadata<BookMetadata>();

            Stasistium.Stages.IStageBaseOutput<Stream> inputWithBookData = input
               .Merge(bookData, (input, data) => input.With(input.Metadata.Add(data.Metadata.TryGetValue<BookMetadata>()!/*We will check for null in the next stage*/)))
               .Where(x => x.Metadata.TryGetValue<BookMetadata>() != null);


            Stasistium.Stages.IStageBaseOutput<MarkdownDocument> insertedMarkdown = inputWithBookData
                .Where(x => x.Id != $".bookdata" && IsMarkdown(x))
                .If(dataFile, x => System.IO.Path.GetExtension(x.Id) == ".xlsx", "Test IF xlsx")
                        .Then((x, _) => x
                        .CacheDocument("excelCache", y => y
                        .ExcelToMarkdownText(true)
                        )
                        .ToStream()
                ).Else((z, dataFile) =>
                    //z.Where(x => System.IO.Path.GetExtension(x.Id) != ".xslt")
                    z.If(dataFile, x => System.IO.Path.GetExtension(x.Id) == ".xslt")
                    .Then((x, dataFile) => x
                            .Xslt(dataFile)
                            .ToStream()
                    ).Else((x, _) => x)

                )


                .Markdown(GenerateMarkdownDocument, name: "Markdown Content")
                    .YamlMarkdownToDocumentMetadata<OrderMarkdownMetadata>()
                    .CrossJoin(key, (x, key) =>
                    {

                        string prefix = NotaPath.Combine("Content", x.Metadata.GetValue<GitRefMetadata>().CalculatedVersion.ToString());
                        string bookPath = NotaPath.Combine(prefix, key.Value);

                        IEnumerable<ImageInline> imageBlocks = x.Value.GetBlocksRecursive()
                                                   .OfType<IInlineContainer>()
                                                   .SelectMany(x => x.GetInlineRecursive())
                                                   .OfType<ImageInline>();
                        foreach (ImageInline item in imageBlocks) {
                            item.Url = NotaPath.Combine(bookPath, NotaPath.GetFolder(x.Id), item.Url);
                            Console.WriteLine($" {x.Id} -> {item.Url}");
                        }
                        return x;
                    })
                .InsertMarkdown()
                ;



            Stasistium.Stages.IStageBaseOutput<string[]> insertedDocuments = insertedMarkdown.ListTransform(x =>
            {
                string[] values = x.SelectMany(y => y.Metadata.TryGetValue<DependendUponMetadata>()?.DependsOn ?? Array.Empty<string>()).Distinct().Where(z => z != null).ToArray();
                //var context = x.First().Context;
                return context.CreateDocument(values, context.GetHashForObject(values), "documentIdsThatAreInserted");
            });

            Stasistium.Stages.IStageBaseOutput<MarkdownDocument> markdown = insertedMarkdown
            // we will remove all docments that are inserted in another.
            .Merge(insertedDocuments, (doc, m) => doc.With(doc.Metadata.Add(new AllDocumentsThatAreDependedOn() { DependsOn = m.Value })))
            .Where(x => !x.Metadata.GetValue<AllDocumentsThatAreDependedOn>().DependsOn.Contains(x.Id))
            .Select(x => x.With(x.Metadata.Remove<AllDocumentsThatAreDependedOn>())) // we remove it so it will late not show changes in documents that do not have changes
            .Stich(1, "stich")
                ;


            Stasistium.Stages.IStageBaseOutput<Stream> nonMarkdown = inputWithBookData
                .Where(x => x.Id != $".bookdata" && !IsMarkdown(x));


            Stasistium.Stages.IStageBaseOutput<string> chapters = markdown.ListTransform(x => context.CreateDocument(string.Empty, string.Empty, "chapters", context.EmptyMetadata.Add(GenerateContentsTable(x))));


            Stasistium.Stages.IStageBaseOutput<MarkdownDocument> referenceLocation = markdown.Merge(chapters, (doc, c) => doc.With(doc.Metadata.Add(c.Metadata)), "merge prepared for render")
                            .Select(x => x.WithId(Path.ChangeExtension(x.Id, ".html")))
                            .GetReferenceLocation();

            Stasistium.Stages.IStageBaseOutput<MarkdownDocument> preparedForRender = referenceLocation
                .CrossJoin(key, (x, key) =>
                 {


                     string prefix = NotaPath.Combine("Content", x.Metadata.GetValue<GitRefMetadata>().CalculatedVersion.ToString());

                     IOrderedEnumerable<ImageReference> newReferences = x.Metadata.GetValue<ImageReferences>().References
                         .Select(y =>
                         {
                             BookVersion calculatedVersion = x.Metadata.GetValue<GitRefMetadata>().CalculatedVersion;
                             BookMetadata? bookMetadata2 = x.Metadata.TryGetValue<BookMetadata>();
                             BookMetadata bookMetadata = x.Metadata.GetValue<BookMetadata>();

                             string DocumentId = NotaPath.Combine(prefix, key.Value, y.Document);
                             return new ImageReference()
                             {
                                 ReferencedId = y.ReferencedId,
                                 Document = DocumentId,
                                 Book = bookMetadata,
                                 Version = calculatedVersion,
                                 Header = y.Header
                             };

                         })
                         .OrderBy(x => x.Document).ThenBy(x => x.Header).ThenBy(x => x.ReferencedId);


                     var images = new ImageReferences()
                     {
                         References = newReferences.ToArray()
                     };

                     return x.With(x.Metadata.AddOrUpdate(images));
                 }, "prepareForRender CorssJoin")
                ;


            //preparedForRender.Merge(preparedForRender.GetReferenceLocations("references"), (stage, file) =>
            //{

            //});

            //var referencesImages = preparedForRender.GetReferenceLocations("references")
            //.Select(x=> {
            //    x.Value.References.Select(y => new ImageReference() { Header = y.Header,
            //     ReferencedId = $"{key}/{y.ReferencedId}",
            //      Document = $"{key}/{y.Document}"
            //    } );
            //})



            Stasistium.Stages.IStageBaseOutput<Stream> markdownRendered = preparedForRender
                .ToHtml(new NotaMarkdownRenderer(editUrl), "Markdown To HTML")
                .FormatXml()
                .ToStream()
                .Silblings();



            Stasistium.Stages.IStageBaseOutput<Stream> concated = markdownRendered
                            .Concat(nonMarkdown);

            Stasistium.Stages.IStageBaseOutput<Stream> stiched = concated
                .CrossJoin(key, (x, key) => x.WithId($"{key.Value}/{x.Id}"), "Stitch corssJoin")
           ;




            Stasistium.Stages.IStageBaseOutput<Stream> changedDocuments = stiched.CrossJoin(key, (x, key) =>
             {
                 string prefix = NotaPath.Combine("Content", x.Metadata.GetValue<GitRefMetadata>().CalculatedVersion.ToString());
                 string bookPath = NotaPath.Combine(prefix, key.Value);
                 IDocument<Stream> changedDocument = ArgumentBookMetadata(x.WithId(NotaPath.Combine(prefix, x.Id)), bookPath);
                 return changedDocument;
             }, "Changed Cross Join");
            return changedDocuments;
        }


        private static bool FileCanHasLicense(IDocument x)
        {
            string extension = NotaPath.GetExtension(x.Id);
            return extension != ".html"
                && extension != ".cshtml"
                && extension != ".css"
                && extension != ".scss"
                && extension != ".xsd"
                && extension != ".xml"
                && extension != ".js"
                && extension != ".meta"
                && extension != ".md"
                ;
        }
        private static IDocument<T> ArgumentBookMetadata<T>(IDocument<T> x, string location)
            where T : class
        {
            BookMetadata? newMetadata = x.Metadata.TryGetValue<BookMetadata>();
            if (newMetadata is null) {
                return x;
            }

            newMetadata = newMetadata.WithLocation(location);

            GitRefMetadata? gitdata = x.Metadata.TryGetValue<GitRefMetadata>();
            if (gitdata != null) {
                newMetadata = newMetadata.WithVersion(gitdata.CalculatedVersion);
            }

            return x.With(x.Metadata.AddOrUpdate(newMetadata));
        }

        private static TableOfContents GenerateContentsTable(ImmutableList<IDocument<MarkdownDocument>> documents)
        {
            if (documents.IsEmpty) {
                return new TableOfContents()
                {
                    Chapters = Array.Empty<TableOfContentsEntry>()
                };
            }
            //var chapters = documents.Select(chapterDocument =>
            TableOfContentsEntry? entry;

            //entry.Page = chapterDocument.Id;
            //entry.Id = string.Empty;
            //entry.Level = 0;

            var stack = new Stack<(MarkdownBlock block, string page)>();

            foreach (IDocument<MarkdownDocument> chapterDocument in documents.Reverse()) {
                PushBlocks(chapterDocument.Value.Blocks, chapterDocument.Id);
            }

            void PushBlocks(IEnumerable<MarkdownBlock> blocks, string page)
            {
                foreach (MarkdownBlock? item in blocks.Reverse()) {
                    stack.Push((item, page));
                }
            }

            var chapterList = new Stack<TableOfContentsEntry>();

            entry = new TableOfContentsEntry
            {
                Page = documents.First().Id,
                Id = string.Empty,
                Title = string.Empty,
                Level = 0
            };
            chapterList.Push(entry);

            while (stack.TryPop(out (MarkdownBlock block, string page) element)) {
                (MarkdownBlock currentBlock, string documentId) = element;
                switch (currentBlock) {
                    case HeaderBlock headerBlock:

                        string id;
                        string title = Stasistium.Stages.MarkdownRenderer.GetHeaderText(headerBlock);
                        if (headerBlock is ChapterHeaderBlock ch && ch.ChapterId != null) {
                            id = ch.ChapterId;
                        } else {
                            id = title;
                        }

                        var currentChapter = new TableOfContentsEntry()
                        {
                            Level = headerBlock.HeaderLevel,
                            Page = documentId,
                            Id = id,
                            Title = title
                        };



                        if (!chapterList.TryPeek(out TableOfContentsEntry? lastChapter)) {
                            lastChapter = null;
                        }

                        if (lastChapter is null) {
                            System.Diagnostics.Debug.Assert(entry is null);
                            chapterList.Push(currentChapter);
                            entry = currentChapter;
                        } else if (lastChapter.Level < currentChapter.Level) {
                            lastChapter.Sections.Add(currentChapter);
                            chapterList.Push(currentChapter);
                        } else {

                            while (lastChapter.Level >= currentChapter.Level) {
                                _ = chapterList.Pop();
                                lastChapter = chapterList.Peek();
                            }

                            if (lastChapter is null) {
                                throw new InvalidOperationException("Should not happen after stich");
                            } else {
                                lastChapter.Sections.Add(currentChapter);
                                chapterList.Push(currentChapter);
                            }
                        }


                        break;
                    case Markdown.Blocks.SoureReferenceBlock insert:
                        PushBlocks(insert.Blocks, documentId);
                        break;
                    default:
                        break;
                }
            }
            if (entry is null) {
                entry = new TableOfContentsEntry
                {
                    Page = documents.First().Id,
                    Id = string.Empty,
                    Level = 0
                };
                chapterList.Push(entry);
            }


            //for (int i = 1; i < chapters.Count; i++)
            //{
            //    if (chapters[i].Level > chapters[i - 1].Level)
            //    {
            //        int j = i + 1;
            //        for (; j < chapters.Count; j++)
            //        {
            //            if (chapters[i].Level > chapters[j].Level)
            //            {
            //                break;
            //            }
            //        }
            //        // we went one index to far
            //        var numberToCopy = j - i - 1;

            //        for (int k = 0; k < numberToCopy; k++)
            //        {
            //            // we do it 
            //            chapters[i - 1].Sections.Add(chapters[i]);
            //            chapters.RemoveAt(i);
            //        }

            //        // we moved th current element to the previous childrean, so we need to lower the index
            //        i--;
            //    }
            //}

            return new TableOfContents()
            {
                Chapters = new[] { entry }
            };
        }

        private static MarkdownDocument GenerateMarkdownDocument()
        {
            return MarkdownDocument.CreateBuilder()
                            .AddBlockParser<Blocks.HeaderBlock.HashParser>()
                            .AddBlockParser<Blocks.ListBlock.Parser>()
                            .AddBlockParser<Markdown.Blocks.ExtendedTableBlock.Parser>()
                            .AddBlockParser<Blocks.TableBlock.Parser>()
                            .AddBlockParser<Blocks.QuoteBlock.Parser>()
                            .AddBlockParser<Blocks.LinkReferenceBlock.Parser>()
                            .AddBlockParser<Markdown.Blocks.InsertBlock.Parser>()
                            .AddBlockParser<Markdown.Blocks.ChapterHeaderBlock.Parser>()
                            .AddBlockParser<Markdown.Blocks.DivBlock.Parser>()
                            .AddBlockParser<Markdown.Blocks.YamlBlock<OrderMarkdownMetadata>.Parser>()
                            .AddBlockParser<Markdown.Blocks.YamlBlock<BookMetadata>.Parser>()
                            .AddBlockParser<Markdown.Blocks.SideNote.Parser>()
                                .Before<TableBlock.Parser>()
                                .Before<Markdown.Blocks.ExtendedTableBlock.Parser>()

                            .AddInlineParser<Inlines.BoldTextInline.ParserAsterix>()
                            .AddInlineParser<Inlines.BoldTextInline.ParserUnderscore>()
                            .AddInlineParser<Inlines.ItalicTextInline.ParserAsterix>()
                            .AddInlineParser<Inlines.ItalicTextInline.ParserUnderscore>()
                            .AddInlineParser<Inlines.EmojiInline.Parser>()
                            .AddInlineParser<Inlines.ImageInline.Parser>()
                            .AddInlineParser<Inlines.MarkdownLinkInline.LinkParser>()
                            .AddInlineParser<Inlines.MarkdownLinkInline.ReferenceParser>()
                            .AddInlineParser<Inlines.StrikethroughTextInline.Parser>()
                            .AddInlineParser<Inlines.LinkAnchorInline.Parser>()

                            .Build();
        }

        private static LibGit2Sharp.Repository PreGit(Config config, LibGit2Sharp.Signature author, string workdirPath, string cache, string output)
        {
            LibGit2Sharp.Repository repo;
            if (Directory.Exists(workdirPath)) {
                try {
                    repo = new LibGit2Sharp.Repository(workdirPath);

                    RepositoryStatus status = repo.RetrieveStatus(new LibGit2Sharp.StatusOptions() { });

                    //if (status.IsDirty)
                    //{
                    //    var currentCommit = repo.Head.Tip;
                    //    repo.Reset(LibGit2Sharp.ResetMode.Hard, currentCommit);

                    //}

                    Remote remote = repo.Network.Remotes["origin"];
                    IEnumerable<string> refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);
                    LibGit2Sharp.Commands.Fetch(repo, remote.Name, refSpecs, null, string.Empty);



                    //LibGit2Sharp.Commands.Pull(repo, author, new LibGit2Sharp.PullOptions() {   MergeOptions = new LibGit2Sharp.MergeOptions() { FastForwardStrategy = LibGit2Sharp.FastForwardStrategy.FastForwardOnly } });

                } catch (Exception e) {
                    Console.Error.WriteLine($"Failed to use old Repo\n{e}");

                    Directory.Delete(workdirPath, true);
                    repo = new LibGit2Sharp.Repository(LibGit2Sharp.Repository.Clone(config.WebsiteRepo?.Url ?? throw new InvalidOperationException($"{nameof(Config.SchemaRepo)} not set on configuration."), workdirPath));

                }
            } else {
                repo = new LibGit2Sharp.Repository(LibGit2Sharp.Repository.Clone(config.WebsiteRepo?.Url ?? throw new InvalidOperationException($"{nameof(Config.SchemaRepo)} not set on configuration."), workdirPath));
            }
            Branch? localMaster = repo.Branches[config.WebsiteRepo?.PrimaryBranchName ?? "master"];
            Branch? originMaster = repo.Branches[$"origin/{config.WebsiteRepo?.PrimaryBranchName ?? "master"}"];
            if (localMaster is null) {

                if (originMaster is null) {
                    throw new InvalidOperationException($"origin is null {string.Join("\n", repo.Branches.Select(x => x.CanonicalName))} ");
                }

                localMaster = repo.CreateBranch(config.WebsiteRepo?.PrimaryBranchName ?? "master", originMaster.Tip);
                _ = repo.Branches.Update(localMaster, b => b.TrackedBranch = originMaster.CanonicalName);
            }
            _ = LibGit2Sharp.Commands.Checkout(repo, localMaster, new LibGit2Sharp.CheckoutOptions()
            {
                CheckoutModifiers = CheckoutModifiers.Force
            });

            repo.Reset(ResetMode.Hard, originMaster.Tip);


            if (Directory.Exists(output)) {
                Directory.Delete(output, true);
            }

            if (Directory.Exists(cache)) {
                Directory.Delete(cache, true);
            }

            _ = Directory.CreateDirectory(output);
            var workDirInfo = new DirectoryInfo(workdirPath);
            foreach (DirectoryInfo? path in workDirInfo.GetDirectories().Where(x => x.Name != ".git")) {
                if (path.Name == "cache") {
                    path.MoveTo(cache);
                } else {
                    path.MoveTo(Path.Combine(output, path.Name));
                }
            }
            foreach (FileInfo path in workDirInfo.GetFiles()) {
                path.MoveTo(Path.Combine(output, path.Name));
            }

            return repo;
        }

        private static void PostGit(LibGit2Sharp.Signature author, LibGit2Sharp.Signature committer, LibGit2Sharp.Repository repo, string branch, string workdirPath, string cache, string output)
        {
            var outputInfo = new DirectoryInfo(output);
            foreach (DirectoryInfo path in outputInfo.GetDirectories()) {
                path.MoveTo(Path.Combine(workdirPath, path.Name));
            }

            foreach (FileInfo path in outputInfo.GetFiles()) {
                path.MoveTo(Path.Combine(workdirPath, path.Name));
            }

            var cacheDirectory = new DirectoryInfo(cache);
            if (cacheDirectory.Exists) {
                cacheDirectory.MoveTo(Path.Combine(workdirPath, cache));
            }

            RepositoryStatus status = repo.RetrieveStatus(new LibGit2Sharp.StatusOptions() { });
            if (status.Any()) {
                IEnumerable<string> filePathsAdded = status.Added.Concat(status.Modified).Concat(status.RenamedInIndex).Concat(status.RenamedInWorkDir).Concat(status.Untracked).Select(mods => mods.FilePath);
                foreach (string? filePath in filePathsAdded) {
                    repo.Index.Add(filePath);
                }

                IEnumerable<string> filePathsRemove = status.Missing.Select(mods => mods.FilePath);
                foreach (string? filePath in filePathsRemove) {
                    repo.Index.Remove(filePath);
                }

                repo.Index.Write();


                // Commit to the repository
                Commit commit = repo.Commit("Updated Build", author, committer, new LibGit2Sharp.CommitOptions() { });
                //repo.Branches.Add()
                ;
                //repo.Network.Push(repo.Network.Remotes["origin"], repo.Head.CanonicalName);
                repo.Network.Push(repo.Head, new LibGit2Sharp.PushOptions() { });
            }
        }

    }

    public class Book
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        private Book()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {

        }

        public Book(string name)
        {
            Name = name;
        }

        public string Name { get; private set; }
    }


}
