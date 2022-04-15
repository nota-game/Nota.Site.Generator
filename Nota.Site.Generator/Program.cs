using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using AdaptMark.Parsers.Markdown;
using AdaptMark.Parsers.Markdown.Blocks;
using Stasistium;
using Stasistium.Documents;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Westwind.AspNetCore.LiveReload;
using Blocks = AdaptMark.Parsers.Markdown.Blocks;
using Inlines = AdaptMark.Parsers.Markdown.Inlines;
using Nota.Site.Generator.Stages;
using Nota.Site.Generator.Markdown.Blocks;
using System.Text;
using AngleSharp;
using AngleSharp.Html.Parser;
using AngleSharp.Dom;

using IDocument = Stasistium.Documents.IDocument;
using LibGit2Sharp;
using System.Reflection;
using AdaptMark.Parsers.Markdown.Inlines;

namespace Nota.Site.Generator
{

    class AllDocumentsThatAreDependedOn
    {
        public string[] DependsOn { get; set; }
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


        static bool CheckLicense(IDocument<Stream> x)
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
            var version = (typeof(Nota).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>() is AssemblyInformationalVersionAttribute attribute)
            ? attribute.InformationalVersion
            : "Unknown";



            const string workdirPath = "gitOut";
            const string cache = "cache";
            const string output = "out";


            configuration ??= new FileInfo("config.json");

            if (!configuration.Exists)
                throw new FileNotFoundException($"No configuration file was found ({configuration.FullName})");

            Config config;
            await using (var context = new GeneratorContext()) {



                var configFile = context.StageFromResult("configuration", configuration.FullName, x => x)
                 .File()
                 .Json<Config>("Parse Configuration")
                 ;

                var taskCompletion = new TaskCompletionSource<Config>();
                configFile.PostStages += (ImmutableList<IDocument<Config?>> cache, Stasistium.Stages.OptionToken options) =>
                {
                    var value = cache.Single().Value;
                    if (value is not null)
                        taskCompletion.SetResult(value);
                    else
                        taskCompletion.SetException(new IOException("Configuration not found"));
                    return Task.CompletedTask;
                };

                await context.Run(new GenerationOptions()
                {

                    Refresh = false,
                    CompressCache = true,
                });
                config = await taskCompletion.Task;
            }

            var editUrl = config.ContentRepo?.Url;
            if (editUrl is not null) {
                if (!editUrl.StartsWith("https://github.com/") || !editUrl.EndsWith(".git"))
                    editUrl = null;
                else {
                    editUrl = editUrl[..^".git".Length] + "/edit/";
                }
            }

            // Create the committer's signature and commit
            var author = new LibGit2Sharp.Signature("NotaSiteGenerator", "@NotaSiteGenerator", DateTime.Now);
            var committer = author;

            var s = System.Diagnostics.Stopwatch.StartNew();


            using var repo = PreGit(config, author, workdirPath, cache, output);
            if (File.Exists(Path.Combine(cache, "sourceVersion"))) {
                var oldVersion = await File.ReadAllTextAsync(Path.Combine(cache, "sourceVersion"));
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

                var configFile = context.StageFromResult("configuration", configuration.FullName, x => x)
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
                        services.AddLiveReload();
                    })
                    .Configure(app =>
                    {
                        app.UseLiveReload();
                        app.UseStaticFiles();
                    })
                    .Build();
                    await host.StartAsync();

                    var feature = host.ServerFeatures.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>();
                    foreach (var item in feature.Addresses)
                        Console.WriteLine($"Listinging to: {item}");
                }



                var contentRepo = configFile
                    .Select(x => x.With(x.Value?.ContentRepo ?? throw x.Context.Exception($"{nameof(Config.ContentRepo)} not set on configuration."), x.Context.GetHashForObject(x.Value.ContentRepo))
                        .With(x.Metadata.Add(new HostMetadata() { Host = x.Value.Host })))
                    .GitClone("Git for Content")
                    //.Where(x => x.Id == "master") // for debuging 
                    //.Where(x => true) // for debuging 
                    ;

                var schemaRepo = configFile
                    .Select(x =>
                        x.With(x.Value?.SchemaRepo ?? throw x.Context.Exception($"{nameof(Config.SchemaRepo)} not set on configuration."), x.Context.GetHashForObject(x.Value.SchemaRepo))
                        .With(x.Metadata.Add(new HostMetadata() { Host = x.Value.Host })))
                    .GitClone("Git for Schema");

                var layoutProvider = configFile
                    .Select(x => x.With(x.Value?.Layouts ?? "layout", x.Value?.Layouts ?? "layout"), "Transform LayoutProvider from Config")
                    .FileSystem("Layout Filesystem")
                    .FileProvider("Layout", "Layout FIle Provider");

                var staticFilesInput = configFile
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

                var staticFiles2 = staticFilesInput
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



                var razorProviderStatic = staticFiles2
                   .FileProvider("Content", "Content file Provider")
                   .Concat(layoutProvider, "Concat Content and layout FileProvider")
                   .RazorProvider("Content", "Layout/ViewStart.cshtml", name: "Razor Provider STATIC with ViewStart");







                var comninedFiles = contentRepo
                    .CacheDocument("branchedDocuments", c => c
                     .GroupBy(x => x.Value.FrindlyName,
                     (key, input) => DocumentsForBranch(input, key, editUrl), "Select Content Files from Ref")
                    ).Debug("files")

                    ;



                var allBooks = comninedFiles.ListTransform(x =>

                {
                    var bookData = x.Where(y => y.Metadata.TryGetValue<SiteMetadata>() is not null).SelectMany(y => y.Metadata.GetValue<SiteMetadata>().Books);
                    var bookArray = bookData.Distinct().ToArray();

                    var books = new AllBooksMetadata()
                    {
                        Books = bookArray
                    };
                    return context.CreateDocument(
                        books, context.GetHashForObject(books), "allBooks"
                        );
                }
                       ); ;

                var imageData = comninedFiles.Where(x => x.Metadata.TryGetValue<ImageReferences>() != null)
              .ListTransform(documents =>
              {
                  var newReferences = documents.SelectMany(x =>
                  {
                      var prefix = NotaPath.Combine("Content", x.Metadata.GetValue<GitRefMetadata>().CalculatedVersion.ToString());
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


                var removedDoubleImages = comninedFiles.Where(x => IsImage(x))
                     .Merge(imageData, (file, image) =>
                     {
                         var references = image.Value.References.Where(x => x.ReferencedId == file.Id);
                         if (references.Any()) {
                             var newData = new ImageReferences()
                             {
                                 References = references.ToArray()
                             };
                             return file.With(file.Metadata.Add(newData));
                         } else return file;
                     })
                    .GroupBy(
                    x =>
                    {
                        // Use file hash. Document Hash has also metadata
                        using (var stream = x.Value)
                            return context.GetHashForStream(stream);
                    },
                    (key, input) =>
                    {
                        var erg = input.ListTransform(key, (x, key) =>
                         {
                             if (!x.Skip(1).Any())
                                 return x.First();

                             var doc = x.First();
                             doc = doc.WithId(key.Single().Value + NotaPath.GetExtension(doc.Id));
                             foreach (var item in x) {
                                 var metadata = item.Metadata.TryGetValue<ImageReferences>();
                                 if (metadata is null) {

                                     metadata = new ImageReferences()
                                     {
                                         References = new[] {new ImageReference() {
                                            ReferencedId = item.Id
                                        } }
                                     };
                                 }
                                 doc = doc.With(doc.Metadata.AddOrUpdate(metadata, (oldvalue, newvalue) => new ImageReferences()
                                 {
                                     References = oldvalue?.References.Concat(newvalue.References).Distinct().ToArray() ?? newvalue.References
                                 }));
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


                var combinedFiles = comninedFiles
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



                var files = combinedFiles.Merge(allBooks, (file, allBooks) => file.With(file.Metadata.Add(allBooks.Value)));




                var licesnseFIles = files
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





                var staticFiles = staticFiles2
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




                var razorProvider = files
                .Debug("test")
                    .FileProvider("Content", "Content file Provider")
                    .Concat(layoutProvider, "Concat Content and layout FileProvider")
                    .RazorProvider("Content", "Layout/ViewStart.cshtml", name: "Razor Provider with ViewStart")
                    ;

                var rendered = files


                        .If(razorProvider
                        , x => Path.GetExtension(x.Id) == ".html")
                            .Then((x, provider) => x
                                    .Debug("before razor")

                                    .Razor(provider)
                                    .ToStream())
                            .Else((x, provider) => x);


                var schemaFiles = schemaRepo

                     .Select(x => x.With(x.Metadata.Add(new GitRefMetadata(x.Value.FrindlyName, x.Value.Type))))
                     .Files(true, name: "Schema Repo to Files")
                     .Where(x => System.IO.Path.GetExtension(x.Id) != ".md")
                     .ToText()
                        .Select(y =>
                        {
                            var gitData = y.Metadata.GetValue<GitRefMetadata>()!;
                            var version = gitData.CalculatedVersion;
                            var host = y.Metadata.GetValue<HostMetadata>()!.Host;
                            var newText = hostReplacementRegex.Replace(y.Value, @$"{host}/schema/{version}/");
                            return y.With(newText, y.Context.GetHashForString(newText));
                        })
                        .ToStream()
                     .Select(x =>
                     {
                         var gitData = x.Metadata.GetValue<GitRefMetadata>()!;
                         var version = gitData.CalculatedVersion;

                         return x.WithId($"schema/{version}/{x.Id.TrimStart('/')}");
                     });

                var rendered2 = rendered
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
                            var feature = host.ServerFeatures.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>();
                            foreach (var item in feature.Addresses)
                                Console.WriteLine($"Listinging to: {item}");
                        } catch (Exception e) {

                            Console.WriteLine("Error");
                            Console.Error.WriteLine(e);
                        }

                        Console.WriteLine("Press Q to Quit, any OTHER key to update.");
                        var key = Console.ReadKey(true);
                        if (key.Key == ConsoleKey.Q)
                            isRunning = false;
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


            PostGit(author, committer, repo, config.ContentRepo?.PrimaryBranchName, workdirPath, cache, output);
            s.Stop();
            Console.WriteLine($"Finishing took {s.Elapsed}");
        }

        private static IDocument<Stream> CombineFiles(IDocument<Stream> input, IDocument<ImmutableList<IDocument<Stream>>> removed)
        {
            var context = input.Context;
            var removedDocuments = removed.Value.Select(y => y.Metadata.TryGetValue<ImageReferences>())
            .Where(x => x != null)
            .SelectMany(x => x.References)
            .Select(x => x.ReferencedId);
            if (removedDocuments.Contains(input.Id))
                return input.WithId($"TO_REMOVE{input.Id}");

            if (Path.GetExtension(input.Id) == ".html") {
                using var stream = input.Value;
                using var reader = new StreamReader(stream);
                string text = reader.ReadToEnd();
                var originalText = text;



                var angleSharpConfig = AngleSharp.Configuration.Default;
                var angleSharpContext = BrowsingContext.New(angleSharpConfig);
                var parser = angleSharpContext.GetService<IHtmlParser>();
                var source = text;
                var document = parser.ParseDocument(source);



                foreach (var removedDocument in removed.Value) {
                    var metadata = removedDocument.Metadata.TryGetValue<ImageReferences>();
                    if (metadata is null)
                        continue;
                    foreach (var item in metadata.References) {
                        var relativeTo = NotaPath.GetFolder(input.Id).AsSpan();
                        var absolute = item.ReferencedId.AsSpan();

                        var clipedRelative = GetFirstPart(relativeTo, out var relativeToRest);
                        var clipedAbsolute = GetFirstPart(absolute, out var absolutRest);

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
                            var index = t.IndexOf('/');
                            if (index == -1) {
                                rest = string.Empty;
                                return t;
                            } else {
                                rest = t[(index + 1)..^0];
                                return t[0..index];
                            }
                        }
                        var absoluteStr = absolute.ToString();
                        // document.QuerySelectorAll($"img[src=\"{absolute.ToString()}\"").Attr("src", "/" + removedDocument.Id);
                        // document.QuerySelectorAll($"img[src=\"{item.ReferencedId}\"").Attr("src", "/" + removedDocument.Id);

                        foreach (var ele in document.All.Where(m => m.LocalName == "img" && m.Attributes.Any(x => x.LocalName == "src" && (x.Value == absoluteStr || x.Value == item.ReferencedId)))) {
                            var licenseData = removedDocument.Metadata.TryGetValue<XmpMetadata>();

                            if (licenseData != null) {
                                if (licenseData.RightsReserved.HasValue)
                                    ele.SetAttribute("rightsReseved", licenseData.RightsReserved?.ToString());
                                if (licenseData.License != null)
                                    ele.SetAttribute("license", Sanitize(licenseData.License));
                                if (licenseData.Creators != null)
                                    ele.SetAttribute("creators", string.Join(", ", licenseData.Creators));
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
                    var data = Encoding.UTF8.GetBytes(text);
                    return input.With(() => new MemoryStream(data), context.GetHashForString(text));
                }
            }

            return input;
        }
        private static Stasistium.Stages.IStageBaseOutput<Stream> DocumentsForBranch(Stasistium.Stages.IStageBaseOutput<GitRefStage> input, Stasistium.Stages.IStageBaseOutput<string> key, string editUrl)
        {
            Stasistium.Stages.IStageBaseOutput<SiteMetadata> siteData;
            Stasistium.Stages.IStageBaseOutput<Stream> grouped;
            //NewMethod(input, context, out siteData, out grouped);
            var context = input.Context;
            var contentFiles = input
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


            var dataFile = contentFiles
                .Where(x => x.Id == "data/nota.xml")
                .Single();


            siteData = contentFiles
                .Where(x => x.Id.EndsWith("/.bookdata"), "only bookdata")
                .Markdown(GenerateMarkdownDocument, "siteData markdown")
                    .YamlMarkdownToDocumentMetadata<BookMetadata>("sitedata YamlMarkdown")

                    .Select(x =>
                    {
                        var startIndex = x.Id.IndexOf('/') + 1;
                        var endIndex = x.Id.IndexOf('/', startIndex);
                        var key = x.Id[startIndex..endIndex];

                        var location = NotaPath.Combine("Content", x.Metadata.GetValue<GitRefMetadata>()!.CalculatedVersion.ToString(), key);

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







            var books = contentFiles.Where(x => x.Id.StartsWith("books/"));
            grouped = books
                .GroupBy
                (dataFile, x =>
                 {
                     var startIndex = x.Id.IndexOf('/') + 1;
                     var endIndex = x.Id.IndexOf('/', startIndex);
                     return x.Id[startIndex..endIndex];
                 }

            , (key, x, dataFile) => GetBooksDocuments(key, x, dataFile, editUrl), "Group by for files");


            var files = grouped
                .Select(x => x.With(x.Metadata.Add(new PageLayoutMetadata() { Layout = "book.cshtml" })))
                .Merge(siteData, (file, y) => file.With(file.Metadata.Add(y.Value)), "Merge SiteData with files")
                .Concat(dataFile.Select(x =>
                {
                    var host = x.Metadata.GetValue<HostMetadata>()!.Host;
                    var newText = hostReplacementRegex.Replace(x.Value.ReadString(), @$"{host}/schema/");
                    var location = NotaPath.Combine("Content", x.Metadata.GetValue<GitRefMetadata>()!.CalculatedVersion.ToString(), x.Id);
                    return x.WithId(location).With(() => newText.ToStream(), x.Context.GetHashForString(newText));
                }))
                //.Merge(siteData, (file, y) => file.With(file.Metadata.Add(y.Value)), "Merge SiteData with files")

                ;


            return files;
        }

        //private static Stasistium.Stages.IStageBaseOutput<Stream> GetBooksDocuments(Stasistium.Stages.IStageBaseOutput<Stream> inputOriginal, string key)
        private static Stasistium.Stages.IStageBaseOutput<Stream> GetBooksDocuments(Stasistium.Stages.IStageBaseOutput<string> key, Stasistium.Stages.IStageBaseOutput<Stream> inputOriginal, Stasistium.Stages.IStageBaseOutput<Stream> dataFile, string editUrl)
        {
            var context = inputOriginal.Context;




            var input = inputOriginal.CrossJoin(key, (x, key) => x.WithId(x.Id[$"books/{key.Value}/".Length..]).With(x.Metadata.Add(new Book(key.Value))), "input chnage cross join");

            var bookData = input.Where(x => x.Id == $".bookdata")
                .Single()
                .Markdown(GenerateMarkdownDocument)
                .YamlMarkdownToDocumentMetadata<BookMetadata>();

             var inputWithBookData = input
                .Merge(bookData, (input, data) => input.With(input.Metadata.Add(data.Metadata.TryGetValue<BookMetadata>()!/*We will check for null in the next stage*/)))
                .Where(x => x.Metadata.TryGetValue<BookMetadata>() != null);


            var insertedMarkdown = inputWithBookData 
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

                        var prefix = NotaPath.Combine("Content", x.Metadata.GetValue<GitRefMetadata>().CalculatedVersion.ToString());
                        var bookPath = NotaPath.Combine(prefix, key.Value);

                        var imageBlocks = x.Value.GetBlocksRecursive()
                                                   .OfType<IInlineContainer>()
                                                   .SelectMany(x => x.GetInlineRecursive())
                                                   .OfType<ImageInline>();
                        foreach (var item in imageBlocks) {
                            item.Url = NotaPath.Combine(bookPath, NotaPath.GetFolder(x.Id), item.Url);
                            Console.WriteLine($" {x.Id} -> {item.Url}");
                        }
                        return x;
                    })
                .InsertMarkdown()
                ;



            var insertedDocuments = insertedMarkdown.ListTransform(x =>
            {
                var values = x.SelectMany(y => y.Metadata.TryGetValue<DependendUponMetadata>()?.DependsOn ?? Array.Empty<string>()).Distinct().Where(z => z != null).ToArray();
                //var context = x.First().Context;
                return context.CreateDocument(values, context.GetHashForObject(values), "documentIdsThatAreInserted");
            });

            var markdown = insertedMarkdown
            // we will remove all docments that are inserted in another.
            .Merge(insertedDocuments, (doc, m) => doc.With(doc.Metadata.Add(new AllDocumentsThatAreDependedOn() { DependsOn = m.Value })))
            .Where(x => !x.Metadata.GetValue<AllDocumentsThatAreDependedOn>().DependsOn.Contains(x.Id))
            .Select(x => x.With(x.Metadata.Remove<AllDocumentsThatAreDependedOn>())) // we remove it so it will late not show changes in documents that do not have changes
            .Stich(1, "stich")
                ;


            var nonMarkdown = inputWithBookData 
                .Where(x => x.Id != $".bookdata" && !IsMarkdown(x));


            var chapters = markdown.ListTransform(x => context.CreateDocument(string.Empty, string.Empty, "chapters", context.EmptyMetadata.Add(GenerateContentsTable(x))));


            var referenceLocation = markdown.Merge(chapters, (doc, c) => doc.With(doc.Metadata.Add(c.Metadata)), "merge prepared for render")
                            .Select(x => x.WithId(Path.ChangeExtension(x.Id, ".html")))
                            .GetReferenceLocation();

            var preparedForRender = referenceLocation
                .CrossJoin(key, (x, key) =>
                 {


                     var prefix = NotaPath.Combine("Content", x.Metadata.GetValue<GitRefMetadata>().CalculatedVersion.ToString());

                     var newReferences = x.Metadata.GetValue<ImageReferences>().References
                         .Select(y =>
                         {
                             BookVersion calculatedVersion = x.Metadata.GetValue<GitRefMetadata>().CalculatedVersion;
                             BookMetadata bookMetadata2 = x.Metadata.TryGetValue<BookMetadata>();
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



            var markdownRendered = preparedForRender
                .ToHtml(new NotaMarkdownRenderer(editUrl), "Markdown To HTML")
                .FormatXml()
                .ToStream()
                .Silblings();



            var concated = markdownRendered
                            .Concat(nonMarkdown);

            var stiched = concated
                .CrossJoin(key, (x, key) => x.WithId($"{key.Value}/{x.Id}"), "Stitch corssJoin")
           ;




            var changedDocuments = stiched.CrossJoin(key, (x, key) =>
             {
                 var prefix = NotaPath.Combine("Content", x.Metadata.GetValue<GitRefMetadata>().CalculatedVersion.ToString());
                 var bookPath = NotaPath.Combine(prefix, key.Value);
                 var changedDocument = ArgumentBookMetadata(x.WithId(NotaPath.Combine(prefix, x.Id)), bookPath);
                 return changedDocument;
             }, "Changed Cross Join");
            return changedDocuments;
        }


        private static bool FileCanHasLicense(IDocument x)
        {
            var extension = NotaPath.GetExtension(x.Id);
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
            var newMetadata = x.Metadata.TryGetValue<BookMetadata>();
            if (newMetadata is null)
                return x;
            newMetadata = newMetadata.WithLocation(location);

            var gitdata = x.Metadata.TryGetValue<GitRefMetadata>();
            if (gitdata != null)
                newMetadata = newMetadata.WithVersion(gitdata.CalculatedVersion);

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
            TableOfContentsEntry? entry = null;

            //entry.Page = chapterDocument.Id;
            //entry.Id = string.Empty;
            //entry.Level = 0;

            var stack = new Stack<(MarkdownBlock block, string page)>();

            foreach (var chapterDocument in documents.Reverse())
                PushBlocks(chapterDocument.Value.Blocks, chapterDocument.Id);

            void PushBlocks(IEnumerable<MarkdownBlock> blocks, string page)
            {
                foreach (var item in blocks.Reverse())
                    stack.Push((item, page));
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

            while (stack.TryPop(out var element)) {
                var (currentBlock, documentId) = element;
                switch (currentBlock) {
                    case HeaderBlock headerBlock:

                        string id;
                        var title = Stasistium.Stages.MarkdownRenderer.GetHeaderText(headerBlock);
                        if (headerBlock is ChapterHeaderBlock ch && ch.ChapterId != null)
                            id = ch.ChapterId;
                        else
                            id = title;



                        var currentChapter = new TableOfContentsEntry()
                        {
                            Level = headerBlock.HeaderLevel,
                            Page = documentId,
                            Id = id,
                            Title = title
                        };



                        if (!chapterList.TryPeek(out var lastChapter))
                            lastChapter = null;
                        if (lastChapter is null) {
                            System.Diagnostics.Debug.Assert(entry is null);
                            chapterList.Push(currentChapter);
                            entry = currentChapter;
                        } else if (lastChapter.Level < currentChapter.Level) {
                            lastChapter.Sections.Add(currentChapter);
                            chapterList.Push(currentChapter);
                        } else {

                            while (lastChapter.Level >= currentChapter.Level) {
                                chapterList.Pop();
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

                    var status = repo.RetrieveStatus(new LibGit2Sharp.StatusOptions() { });

                    //if (status.IsDirty)
                    //{
                    //    var currentCommit = repo.Head.Tip;
                    //    repo.Reset(LibGit2Sharp.ResetMode.Hard, currentCommit);

                    //}

                    var remote = repo.Network.Remotes["origin"];
                    var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);
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
            var localMaster = repo.Branches[config.WebsiteRepo?.PrimaryBranchName ?? "master"];
            var originMaster = repo.Branches[$"origin/{config.WebsiteRepo?.PrimaryBranchName ?? "master"}"];
            if (localMaster is null) {

                if (originMaster is null) {
                    throw new InvalidOperationException($"origin is null {string.Join("\n", repo.Branches.Select(x => x.CanonicalName))} ");
                }

                localMaster = repo.CreateBranch(config.WebsiteRepo?.PrimaryBranchName ?? "master", originMaster.Tip);
                repo.Branches.Update(localMaster, b => b.TrackedBranch = originMaster.CanonicalName);
            }
            LibGit2Sharp.Commands.Checkout(repo, localMaster, new LibGit2Sharp.CheckoutOptions()
            {
                CheckoutModifiers = CheckoutModifiers.Force
            });

            repo.Reset(ResetMode.Hard, originMaster.Tip);


            if (Directory.Exists(output))
                Directory.Delete(output, true);

            if (Directory.Exists(cache))
                Directory.Delete(cache, true);

            Directory.CreateDirectory(output);
            var workDirInfo = new DirectoryInfo(workdirPath);
            foreach (var path in workDirInfo.GetDirectories().Where(x => x.Name != ".git")) {
                if (path.Name == "cache")
                    path.MoveTo(cache);
                else
                    path.MoveTo(Path.Combine(output, path.Name));
            }
            foreach (var path in workDirInfo.GetFiles()) {
                path.MoveTo(Path.Combine(output, path.Name));
            }

            return repo;
        }

        private static void PostGit(LibGit2Sharp.Signature author, LibGit2Sharp.Signature committer, LibGit2Sharp.Repository repo, string branch, string workdirPath, string cache, string output)
        {
            var outputInfo = new DirectoryInfo(output);
            foreach (var path in outputInfo.GetDirectories())
                path.MoveTo(Path.Combine(workdirPath, path.Name));
            foreach (var path in outputInfo.GetFiles())
                path.MoveTo(Path.Combine(workdirPath, path.Name));

            var cacheDirectory = new DirectoryInfo(cache);
            if (cacheDirectory.Exists)
                cacheDirectory.MoveTo(Path.Combine(workdirPath, cache));


            var status = repo.RetrieveStatus(new LibGit2Sharp.StatusOptions() { });
            if (status.Any()) {
                var filePathsAdded = status.Added.Concat(status.Modified).Concat(status.RenamedInIndex).Concat(status.RenamedInWorkDir).Concat(status.Untracked).Select(mods => mods.FilePath);
                foreach (var filePath in filePathsAdded)
                    repo.Index.Add(filePath);

                var filePathsRemove = status.Missing.Select(mods => mods.FilePath);
                foreach (var filePath in filePathsRemove)
                    repo.Index.Remove(filePath);

                repo.Index.Write();


                // Commit to the repository
                var commit = repo.Commit("Updated Build", author, committer, new LibGit2Sharp.CommitOptions() { });
                //repo.Branches.Add()
                ;
                //repo.Network.Push(repo.Network.Remotes["origin"], repo.Head.CanonicalName);
                repo.Network.Push(repo.Head, new LibGit2Sharp.PushOptions() { });
            }
        }

    }

    public class Book
    {
        private Book()
        {

        }

        public Book(string name)
        {
            this.Name = name;
        }

        public string Name { get; private set; }
    }

}
