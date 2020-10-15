﻿using Microsoft.AspNetCore.Builder;
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

namespace Nota.Site.Generator
{

    class AllDocumentsThatAreDependedOn
    {
        public string[] DependsOn { get; set; }
    }
    class Program
    {
        private static IWebHost? host;


        private static readonly string[] MarkdownExtensions = { ".md", ".xlsx", ".xslt" };

        private static bool IsMarkdown(IDocument document)
        {
            return MarkdownExtensions.Any(x => Path.GetExtension(document.Id) == x);
        }

        /// <summary>
        /// The Main Method
        /// </summary>
        /// <param name="configuration">Path to the configuration file (json)</param>
        /// <param name="serve">Set to start dev server</param>
        static async Task Main(FileInfo? configuration = null, bool serve = false)
        {

            const string workdirPath = "gitOut";
            const string cache = "cache";
            const string output = "out";


            configuration ??= new FileInfo("config.json");

            if (!configuration.Exists)
                throw new FileNotFoundException($"No configuration file was found ({configuration.FullName})");

            Config config;
            await using (var context = new GeneratorContext())
            {



                var configFile = context.StageFromResult("configuration", configuration.FullName, x => x)
                 .File()
                 .Json("Parse Configuration")
                 .For<Config>();

                config = (await (await configFile.DoIt(null, new GenerationOptions()
                {
                    Refresh = false,
                    CompressCache = true,
                }.Token)).Perform).Value;
            }

            // Create the committer's signature and commit
            var author = new LibGit2Sharp.Signature("NotaSiteGenerator", "@NotaSiteGenerator", DateTime.Now);
            var committer = author;

            var s = System.Diagnostics.Stopwatch.StartNew();


            using var repo = PreGit(config, author, workdirPath, cache, output);
            await using (var context = new GeneratorContext())
            {

                var configFile = context.StageFromResult("configuration", configuration.FullName, x => x)
                 .File()
                 .Json("Parse Configuration")
                 .For<Config>();

                if (serve)
                {
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
                    .Transform(x => x.With(x.Value.ContentRepo ?? throw x.Context.Exception($"{nameof(Config.ContentRepo)} not set on configuration."), x.Value.ContentRepo))
                    .GitClone("Git for Content")
                    .Where(x => x.Id == "master") // for debuging 
                                                  //.Where(x => true) // for debuging 
                    ;

                var schemaRepo = configFile
                    .Transform(x => x.With(x.Value.SchemaRepo ?? throw x.Context.Exception($"{nameof(Config.SchemaRepo)} not set on configuration."), x.Value.SchemaRepo)
                        .With(x.Metadata.Add(new HostMetadata() { Host = x.Value.Host })))
                    .GitClone("Git for Schema");

                var layoutProvider = configFile
                    .Transform(x => x.With(x.Value.Layouts ?? "layout", x.Value.Layouts ?? "layout"), "Transform LayoutProvider from Config")
                    .FileSystem("Layout Filesystem")
                    .FileProvider("Layout", "Layout FIle Provider");

                var staticFilesInput = configFile
                    .Transform(x => x.With(x.Value.StaticContent ?? "static", x.Value.StaticContent ?? "static"), "Static FIle from Config")
                    .FileSystem("static Filesystem");



                var licesnseFIles = contentRepo.SelectMany(input =>
                    input.GitRefToFiles(name: "Read Files from Git (Licenses)")
                    .Where(x =>
                    {
                        var extension = Path.GetExtension(x.Id);
                        return extension != ".html"
                            && extension != ".cshtml"
                            && extension != ".css"
                            && extension != ".scss"
                            && extension != ".js"
                            && extension != ".md"
                            ;
                    })
                 );



                var generatorOptions = new GenerationOptions()
                {
                    CompressCache = false,
                    Refresh = true
                };


                var contentFiles = contentRepo
                    .SelectMany(input =>
                    {
                        var startData = input
                        .Transform(x => x.With(x.Metadata.Add(new GitMetadata(x.Value.FrindlyName, x.Value.Type))), "Add GitMetada (Content)")
                        .GitRefToFiles(name: "Read Files from Git (Content)")
                            .Sidecar()
                                .For<BookMetadata>(".metadata")
                            ;
                        return startData;
                    }, "Select Content Files from Ref");

                var dataFile = contentFiles
                    .Where(x => x.Id == "data/nota.xml")
                    .SingleEntry();


                var siteData = contentFiles
                    .Where(x => x.Id.EndsWith("/.bookdata"), "only bookdata")
                    .Select(input2 =>
                        input2.Markdown(GenerateMarkdownDocument, "siteData markdown")
                        .YamlMarkdownToDocumentMetadata("sitedata YamlMarkdown")
                            .For<BookMetadata>()
                        .Transform(x =>
                        {
                            var startIndex = x.Id.IndexOf('/') + 1;
                            var endIndex = x.Id.IndexOf('/', startIndex);
                            var key = x.Id[startIndex..endIndex];

                            var location = NotaPath.Combine("Content", x.Metadata.GetValue<GitMetadata>()!.CalculatedVersion.ToString(), key);

                            return ArgumentBookMetadata(x, location)
                                .WithId(location);
                        }))
                    .Where(x => x.Metadata.TryGetValue<BookMetadata>() != null, "filter book in sitedata without Bookdata")
                    .ListToSingle(input =>
                    {
                        var siteMetadata = new SiteMetadata()
                        {
                            Books = input.Select(x => x.Metadata.GetValue<BookMetadata>()).ToArray()
                        };
                        return context.CreateDocument(siteMetadata, context.GetHashForObject(siteMetadata), "siteMetadata");
                    }, "make siteData to single element");




                var sassFiles = staticFilesInput
             .Where(x => Path.GetExtension(x.Id) == ".scss", "Filter .scss")
             .Select(x => x.ToText(name: "actual to text for scss"), "scss transfrom to text");

                var staticFiles2 = staticFilesInput
                       .SetVariable(sassFiles)
                       .Select(input =>
                           input.If(x => Path.GetExtension(x.Id) == ".scss")
                           .Then(x =>
                               x.ToText()
                               .GetVariable(sassFiles, (y, sass) => y.Sass(sass))
                               .TextToStream())
                           .Else(x => x.GetVariable(sassFiles, (y, _) => y))
                       )
                .Merge(siteData, (file, y) => file.With(file.Metadata.Add(y.Value)), "Merge SiteData with files");




                var razorProviderStatic = staticFiles2
                    .FileProvider("Content", "Content file Provider")
                    .Concat(layoutProvider, "Concat Content and layout FileProvider")
                    .RazorProvider("Content", "Layout/ViewStart.cshtml", name: "Razor Provider STATIC with ViewStart");

                var staticFiles = staticFiles2
                    .SetVariable(razorProviderStatic)
                    .Select(input => input
                        .If(x => Path.GetExtension(x.Id) == ".cshtml")
                            .Then(x => x
                                .GetVariable(razorProviderStatic, (y, provider) => y
                                    .Razor(provider)
                                    .TextToStream()
                                    .Transform(doc => doc.WithId(Path.ChangeExtension(doc.Id, ".html")))))
                            .Else(x => x.GetVariable(razorProviderStatic, (y, _) => y)));








                var grouped = contentFiles.Where(x => x.Id.StartsWith("books/")).GroupBy
                    (x =>
                    {
                        var startIndex = x.Id.IndexOf('/') + 1;
                        var endIndex = x.Id.IndexOf('/', startIndex);
                        return x.Id[startIndex..endIndex];
                    }

                , (inputOriginal, key) =>
                {
                    var input = inputOriginal.Transform(x => x.WithId(x.Id.Substring($"books/{key}/".Length)));

                    var bookData = input.Where(x => x.Id == $".bookdata")
                        .SingleEntry()
                        .Markdown(GenerateMarkdownDocument)
                        .YamlMarkdownToDocumentMetadata()
                            .For<BookMetadata>();

                    var insertedMarkdown = input
                        .Where(x => x.Id != $".bookdata" && IsMarkdown(x))
                        .Select(data =>
                            data.If(x => System.IO.Path.GetExtension(x.Id) == ".xlsx")
                                .Then(x => x
                                .ExcelToMarkdownText()
                                .TextToStream()
                        ).Else(z => z.If(x => System.IO.Path.GetExtension(x.Id) == ".xslt")
                                .Then(x => x
                                .Xslt(dataFile)
                                .TextToStream()
                        ).Else(x => x)

                        ))
                        .Select(x => x.Markdown(GenerateMarkdownDocument, name: "Markdown Content")
                            .YamlMarkdownToDocumentMetadata()
                                        .For<OrderMarkdownMetadata>()
                                        )
                        .InsertMarkdown();

                    var insertedDocuments = insertedMarkdown.ListToSingle(x =>
                    {
                        var values = x.SelectMany(y => y.Metadata.TryGetValue<DependendUponMetadata>()?.DependsOn ?? Array.Empty<string>()).Distinct().Where(z => z != null).ToArray();
                        var context = x.First().Context;
                        return context.CreateDocument(values, context.GetHashForObject(values), "documentIdsThatAreInserted");
                    })
                        ;

                    var markdown = insertedMarkdown
                    // we will remove all docments that are inserted in another.
                    .Merge(insertedDocuments, (doc, m) => doc.With(doc.Metadata.Add(new AllDocumentsThatAreDependedOn() { DependsOn = m.Value })))
                    .Where(x => !x.Metadata.GetValue<AllDocumentsThatAreDependedOn>().DependsOn.Contains(x.Id))
                    .Transform(x => x.With(x.Metadata.Remove<AllDocumentsThatAreDependedOn>())) // we remove it so it will late not show changes in documents that do not have changes
                        .Stich(2, "stich")
                        ;
                    var nonMarkdown = input
                        .Where(x => x.Id != $".bookdata" && !IsMarkdown(x));

                    var chapters = markdown.ListToSingle(x => context.CreateDocument(string.Empty, string.Empty, "chapters", context.EmptyMetadata.Add(GenerateContentsTable(x))));

                    var markdownRendered = markdown.Merge(chapters, (doc, c) => doc.With(doc.Metadata.Add(c.Metadata)))
                        .Select(x => x.MarkdownToHtml(new NotaMarkdownRenderer(), "Markdown To HTML")
                        .Transform(x => x.WithId(Path.ChangeExtension(x.Id, ".html")))
                        .FormatXml()
                        .TextToStream(), "Markdown All")
                        .Silblings();



                    var stiched = markdownRendered
                        .Concat(nonMarkdown)
                        .Transform(x => x.WithId($"{key}/{x.Id}"))
                        .Merge(bookData, (input, data) => input.With(input.Metadata.Add(data.Metadata.TryGetValue<BookMetadata>()!/*We will check for null in the next stage*/)))
                        .Where(x => x.Metadata.TryGetValue<BookMetadata>() != null);



                    var changedDocuments = stiched.Select(y => y.Transform(x =>
                    {
                        var prefix = NotaPath.Combine("Content", x.Metadata.GetValue<GitMetadata>().CalculatedVersion.ToString());
                        var bookPath = NotaPath.Combine(prefix, key);
                        var changedDocument = ArgumentBookMetadata(x.WithId(NotaPath.Combine(prefix, x.Id)), bookPath);
                        return changedDocument;
                    }));

                    return changedDocuments;
                }, "Group by for files");



                var files = grouped
                    .Transform(x => x.With(x.Metadata.Add(new PageLayoutMetadata() { Layout = "book.cshtml" })))
                    .Merge(siteData, (file, y) => file.With(file.Metadata.Add(y.Value)), "Merge SiteData with files")
                    ;



                var razorProvider = files
                    .FileProvider("Content", "Content file Provider")
                    .Concat(layoutProvider, "Concat Content and layout FileProvider")
                    .RazorProvider("Content", "Layout/ViewStart.cshtml", name: "Razor Provider with ViewStart");

                var rendered = files
                    .SetVariable(razorProvider)
                    .Select(input => input
                        .If(x => Path.GetExtension(x.Id) == ".html")
                            .Then(x => x
                                .GetVariable(razorProvider, (y, provider) => y
                                    .Razor(provider).TextToStream()))
                            .Else(x => x.GetVariable(razorProvider, (y, _) => y)));


                var hostReplacementRegex = new System.Text.RegularExpressions.Regex(@"(?<host>http://nota\.org)/schema/", System.Text.RegularExpressions.RegexOptions.Compiled);
                var schemaFiles = schemaRepo
                    .SelectMany(input =>
                     input
                     .Transform(x => x.With(x.Metadata.Add(new GitMetadata(x.Value.FrindlyName, x.Value.Type))))
                     .GitRefToFiles()
                     .Where(x => System.IO.Path.GetExtension(x.Id) != ".md")
                     .Select(x =>
                        x.ToText()
                        .Transform(y =>
                        {
                            var gitData = y.Metadata.GetValue<GitMetadata>()!;
                            var version = gitData.CalculatedVersion;
                            var host = y.Metadata.GetValue<HostMetadata>()!.Host;
                            var newText = hostReplacementRegex.Replace(y.Value, @$"{host}/schema/{version}/");
                            return y.With(newText, y.Context.GetHashForString(newText));
                        })
                        .TextToStream()
                     )
                     .Transform(x =>
                     {
                         var gitData = x.Metadata.GetValue<GitMetadata>()!;
                         var version = gitData.CalculatedVersion;

                         return x.WithId($"schema/{version}/{x.Id.TrimStart('/')}");
                     }));

                var rendered2 = rendered
                    ;

                var g = rendered2
                    //.Transform(x => x.WithId(Path.ChangeExtension(x.Id, ".html")))
                    .Concat(schemaFiles)
                    .Concat(staticFiles)
                    .Persist(new DirectoryInfo(output), generatorOptions)
                    ;

                if (host != null)
                {

                    s.Stop();
                    context.Logger.Info($"Preperation Took {s.Elapsed}");

                    bool isRunning = true;

                    Console.CancelKeyPress += (sender, e) => isRunning = false;
                    bool forceUpdate = false;
                    while (isRunning)
                    {
                        try
                        {
                            Console.Clear();
                            s.Restart();
                            await g.UpdateFiles(forceUpdate).ConfigureAwait(false);
                            s.Stop();
                            context.Logger.Info($"Update Took {s.Elapsed}");
                            var feature = host.ServerFeatures.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>();
                            foreach (var item in feature.Addresses)
                                Console.WriteLine($"Listinging to: {item}");
                        }
                        catch (Exception e)
                        {

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


                }
                else
                {
                    await g.UpdateFiles().ConfigureAwait(false);
                }

            }


            PostGit(author, committer, repo, workdirPath, cache, output);
            s.Stop();
            Console.WriteLine($"Finishing took {s.Elapsed}");
        }

        private static IDocument<T> ArgumentBookMetadata<T>(IDocument<T> x, string location)
            where T : class
        {
            var newMetadata = x.Metadata.TryGetValue<BookMetadata>();
            if (newMetadata is null)
                return x;
            newMetadata = newMetadata.WithLocation(location);

            var gitdata = x.Metadata.TryGetValue<GitMetadata>();
            if (gitdata != null)
                newMetadata = newMetadata.WithVersion(gitdata.CalculatedVersion);

            return x.With(x.Metadata.AddOrUpdate(newMetadata));
        }

        private static TableOfContents GenerateContentsTable(ImmutableList<IDocument<MarkdownDocument>> documents)
        {
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
                Level = 0
            };
            chapterList.Push(entry);

            while (stack.TryPop(out var element))
            {
                var (currentBlock, documentId) = element;
                switch (currentBlock)
                {
                    case HeaderBlock headerBlock:

                        var id = Stasistium.Stages.MarkdownRenderer.GetHeaderText(headerBlock);

                        var currentChapter = new TableOfContentsEntry()
                        {
                            Level = headerBlock.HeaderLevel,
                            Page = documentId,
                            Id = id
                        };



                        if (!chapterList.TryPeek(out var lastChapter))
                            lastChapter = null;
                        if (lastChapter is null)
                        {
                            System.Diagnostics.Debug.Assert(entry is null);
                            chapterList.Push(currentChapter);
                            entry = currentChapter;
                        }
                        else if (lastChapter.Level < currentChapter.Level)
                        {
                            lastChapter.Sections.Add(currentChapter);
                            chapterList.Push(currentChapter);
                        }
                        else
                        {

                            while (lastChapter.Level >= currentChapter.Level)
                            {
                                chapterList.Pop();
                                lastChapter = chapterList.Peek();
                            }

                            if (lastChapter is null)
                            {
                                throw new InvalidOperationException("Should not happen after stich");
                            }
                            else
                            {
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
            if (entry is null)
            {
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
            if (Directory.Exists(workdirPath))
            {
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


                var originMaster = repo.Branches["origin/master"];
                repo.Reset(LibGit2Sharp.ResetMode.Hard, originMaster.Tip);

                //LibGit2Sharp.Commands.Pull(repo, author, new LibGit2Sharp.PullOptions() {   MergeOptions = new LibGit2Sharp.MergeOptions() { FastForwardStrategy = LibGit2Sharp.FastForwardStrategy.FastForwardOnly } });

            }
            else
                repo = new LibGit2Sharp.Repository(LibGit2Sharp.Repository.Clone(config.WebsiteRepo ?? throw new InvalidOperationException($"{nameof(Config.SchemaRepo)} not set on configuration."), workdirPath));

            if (Directory.Exists(output))
                Directory.Delete(output, true);

            if (Directory.Exists(cache))
                Directory.Delete(cache, true);

            Directory.CreateDirectory(output);
            var workDirInfo = new DirectoryInfo(workdirPath);
            foreach (var path in workDirInfo.GetDirectories().Where(x => x.Name != ".git"))
            {
                if (path.Name == "cache")
                    path.MoveTo(cache);
                else
                    path.MoveTo(Path.Combine(output, path.Name));
            }
            foreach (var path in workDirInfo.GetFiles())
            {
                path.MoveTo(Path.Combine(output, path.Name));
            }

            return repo;
        }

        private static void PostGit(LibGit2Sharp.Signature author, LibGit2Sharp.Signature committer, LibGit2Sharp.Repository repo, string workdirPath, string cache, string output)
        {
            var outputInfo = new DirectoryInfo(output);
            foreach (var path in outputInfo.GetDirectories())
                path.MoveTo(Path.Combine(workdirPath, path.Name));
            foreach (var path in outputInfo.GetFiles())
                path.MoveTo(Path.Combine(workdirPath, path.Name));

            new DirectoryInfo(cache).MoveTo(Path.Combine(workdirPath, cache));


            var status = repo.RetrieveStatus(new LibGit2Sharp.StatusOptions() { });
            if (status.Any())
            {
                var filePathsAdded = status.Added.Concat(status.Modified).Concat(status.RenamedInIndex).Concat(status.RenamedInWorkDir).Concat(status.Untracked).Select(mods => mods.FilePath);
                foreach (var filePath in filePathsAdded)
                    repo.Index.Add(filePath);

                var filePathsRemove = status.Missing.Select(mods => mods.FilePath);
                foreach (var filePath in filePathsRemove)
                    repo.Index.Remove(filePath);

                repo.Index.Write();


                // Commit to the repository
                var commit = repo.Commit("Updated Build", author, committer, new LibGit2Sharp.CommitOptions() { });

                repo.Network.Push(repo.Branches["master"], new LibGit2Sharp.PushOptions() { });
            }
        }

    }

    public class ContentVersions
    {
        private readonly IEnumerable<BookVersion> enumerable;

        public ContentVersions(IEnumerable<BookVersion> enumerable)
        {
            this.enumerable = enumerable.ToArray();
        }

        public IEnumerable<BookVersion> Versions => this.enumerable;
    }


    public class Config
    {
        public string? ContentRepo { get; set; }
        public string? SchemaRepo { get; set; }
        public string? Layouts { get; set; }
        public string? StaticContent { get; set; }
        public string? Host { get; set; }

        public string? WebsiteRepo { get; set; }
    }

    internal class GitMetadata
    {
        public GitMetadata(GitRefStage value)
        {
            this.Name = value.FrindlyName;
            this.Type = value.Type;
        }

        public GitMetadata(string name, GitRefType type)
        {
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
            this.Type = type;
        }

        public string Name { get; }
        public GitRefType Type { get; }

        public BookVersion CalculatedVersion
        {
            get
            {
                BookVersion version;
                if (this.Type == GitRefType.Branch && this.Name == "master")
                    version = BookVersion.VNext;
                else if (this.Type == GitRefType.Branch)
                    version = new BookVersion(true, this.Name);
                else
                    version = new BookVersion(false, this.Name);
                return version;
            }
        }

    }

    public enum BookType : byte
    {
        Undefined = 0,
        Rule = 1,
        Source = 2,
        Story = 3,
    }

    public static class BookTypeExtension
    {
        public static string ToId(this BookType bookType) => bookType switch
        {
            BookType.Undefined => "UNDEFINED",
            BookType.Rule => "R",
            BookType.Source => "Q",
            BookType.Story => "A",
            _ => "UNKNOWN"
        };
    }

    public class BookMetadata
    {
        public BookMetadata()
        {

        }
        public BookMetadata(string? location = null, string? beginning = null, BookVersion version = default)
        {
            this.Location = location;
            this.Beginning = beginning;
            this.Version = version;
        }

        // From file
        /// <summary>
        /// The Title of the book
        /// </summary>
        public string Title { get; set; }
        /// <summary>
        /// The Number of the book.
        /// </summary>
        public uint Number { get; set; }
        /// <summary>
        /// The Id of the cover
        /// </summary>
        public string Cover { get; set; }
        /// <summary>
        /// The type of book
        /// </summary>
        public BookType BookType { get; set; }
        /// <summary>
        /// An optional abbreviation.
        /// </summary>
        public string? Abbr { get; set; }
        /// <summary>
        /// The Abstract of this book formated as markdown
        /// </summary>
        public string Abstract { get; set; }


        // Generated
        public string? Location { get; }
        public string? Beginning { get; }
        public BookVersion Version { get; }


        public BookMetadata WithLocation(string location) => new BookMetadata(location, this.Beginning, this.Version)
        {
            Title = this.Title,
            Number = this.Number,
            Cover = this.Cover,
            BookType = this.BookType,
            Abbr = this.Abbr,
            Abstract = this.Abstract,
        };
        public BookMetadata WithBeginning(string beginning) => new BookMetadata(this.Location, beginning, this.Version)
        {
            Title = this.Title,
            Number = this.Number,
            Cover = this.Cover,
            BookType = this.BookType,
            Abbr = this.Abbr,
            Abstract = this.Abstract,
        };
        public BookMetadata WithVersion(BookVersion version) => new BookMetadata(this.Location, this.Beginning, version)
        {
            Title = this.Title,
            Number = this.Number,
            Cover = this.Cover,
            BookType = this.BookType,
            Abbr = this.Abbr,
            Abstract = this.Abstract,
        };
    }

    internal class HostMetadata
    {
        public string? Host { get; set; }
    }

    /// <summary>
    /// Contains the layout that should be used
    /// </summary>
    public class PageLayoutMetadata
    {
        /// <summary>
        /// The Layout that should be used.
        /// </summary>
        public string? Layout { get; set; }
    }

    public class SiteMetadata
    {
        public IList<BookMetadata> Books { get; set; }
    }
}
