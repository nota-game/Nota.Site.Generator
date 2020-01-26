using Microsoft.Toolkit.Parsers.Markdown;
using Stasistium;
using Stasistium.Documents;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Blocks = Microsoft.Toolkit.Parsers.Markdown.Blocks;
using Inlines = Microsoft.Toolkit.Parsers.Markdown.Inlines;

namespace Nota.Site.Generator
{
    class Program
    {


        /// <summary>
        /// The Main Method
        /// </summary>
        /// <param name="configuration">Path to the configuration file (json)</param>
        static async Task Main(FileInfo? configuration = null)
        {
            configuration ??= new FileInfo("config.json");

            if (!configuration.Exists)
                throw new FileNotFoundException($"No configuration file was found ({configuration.FullName})");

            await using var context = new GeneratorContext();

            var configFile = context.StageFromResult(configuration.FullName, x => x)
             .File()
             .Json("Parse Configuration")
             .For<Config>();

            var config = (await (await configFile.DoIt(null, new GenerationOptions().Token)).Perform).result.Value;

            // Create the committer's signature and commit
            var author = new LibGit2Sharp.Signature("NotaSiteGenerator", "@NotaSiteGenerator", DateTime.Now);
            var committer = author;


            const string workdirPath = "gitOut";
            const string cache = "cache";
            const string output = "out";

            using var repo = PreGit(context, config, author, workdirPath, cache, output);



            var contentRepo = configFile
                .Transform(x => x.With(x.Value.ContentRepo ?? throw x.Context.Exception($"{nameof(Config.ContentRepo)} not set on configuration."), x.Value.ContentRepo))
                .GitModul("Git for Content");

            var schemaRepo = configFile
                .Transform(x => x.With(x.Value.SchemaRepo ?? throw x.Context.Exception($"{nameof(Config.SchemaRepo)} not set on configuration."), x.Value.SchemaRepo)
                .With(x.Metadata.Add(new HostMetadata() { Host = x.Value.Host })))
                .GitModul("Git for Schema");

            var layoutProvider = configFile
                .Transform(x => x.With(x.Value.Layouts ?? "layout", x.Value.Layouts ?? "layout"))
                .FileSystem("Layout Filesystem")
                .FileProvider("Layout", "Layout FIle Provider");

            var generatorOptions = new GenerationOptions()
            {
                CompressCache = false,
                Refresh = true
            };
            var s = System.Diagnostics.Stopwatch.StartNew();

            var contentVersions = contentRepo.Transform(input =>
            {
                var gitData = new GitMetadata(input.Value);
                string version = gitData.CalculatedVersion;
                return input.With(version, version).WithId(version);
            })
                .OrderBy(x => new VersionComparer(x.Id))
                .ListToSingle(x => context.Create("", "", "ContentVersions", context.EmptyMetadata.Add(new ContentVersions(x.Select(x => x.Value)))));


            var files = contentRepo
                .SelectMany(input =>
                {
                    var startData = input
                    .Transform(x => x.With(x.Metadata.Add(new GitMetadata(x.Value.FrindlyName, x.Value.Type))), "Add GitMetada (Content)")
                    .GitRefToFiles("Read Files from Git (Content)")
                        //.Sidecar()
                        //    .For<BookMetadata>(".metadata")
                        ;


                    var grouped = startData.Where(x => x.Id.StartsWith("books/")).GroupBy
                        (x =>
                        {
                            var startIndex = x.Id.IndexOf('/') + 1;
                            var endIndex = x.Id.IndexOf('/', startIndex);
                            return x.Id[startIndex..endIndex];
                        }

                    , (input, key) =>
                    {

                        var bookData = input.Where(x => x.Id == $"books/{key}/.bookdata")
                            .SingleEntry()
                            .Markdown(GenerateMarkdownDocument)
                            .YamlMarkdownToDocumentMetadata()
                                .For<BookMetadata>();

                        var excel = input
                            .Where(x => System.IO.Path.GetExtension(x.Id) == ".xlsx")
                            .Select(x => x
                                .ExcelToMarkdownText()
                                .Markdown(GenerateMarkdownDocument, name: "Markdown Excel"));

                        var markdown = input
                            .Where(x => System.IO.Path.GetExtension(x.Id) == ".md")
                            .Select(x => x.Markdown(GenerateMarkdownDocument, name: "Markdown Content"));

                        var combined = markdown.Concat(excel)
                            .Select(x => x.YamlMarkdownToDocumentMetadata()
                                            .For<OrderMarkdownMetadata>());

                        var inserted = combined.Select(input => input.InsertMarkdown(combined));


                        var stiched = inserted.Stich("stich")
                            .Transform(x => x.WithId($"{key}/{x.Id}"))
                            .Merge(bookData, (input, data) => input.With(input.Metadata.Add(data.Metadata.GetValue<BookMetadata>())));



                        return stiched;
                    });


                    var result = grouped
                        .Select(x => x.MarkdownToHtml(new NotaMarkdownRenderer(), "Markdown To HTML")
                            .TextToStream(), "Markdown All");


                    return result;
                }, "Working on content branches")
                .Select(x => x.Transform(x => x.WithId(Path.Combine("Content", x.Metadata.GetValue<GitMetadata>()!.CalculatedVersion, x.Id)), "Content Id Change"))

            //.SelectMany(x=>
            //                x.GitRefToFiles("Read Files from Git (Content)"))

                .Merge(contentVersions, (x1, x2) => x1.With(x1.Metadata.Add(x2.Metadata.GetValue<ContentVersions>() ?? throw new InvalidOperationException("Should Not Happen"))));



            var razorProvider = files
                .FileProvider("Content", "Content file Provider")
                .Concat(layoutProvider, "Concat Content and layout FileProvider")
                .RazorProvider("Content", "Layout/ViewStart.cshtml", name: "Razor Provider with ViewStart");

            var rendered = files.Select(razorProvider,(x, provider) => x.Razor(provider).TextToStream());
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
                 })

                        );

            var rendered2 = rendered
                .Where(x => true)



                ;

            var g = rendered2
                .Transform(x => x.WithId(Path.ChangeExtension(x.Id, ".html")))
                //.Concat(schemaFiles)
                .Persist(new DirectoryInfo(output), generatorOptions)
                ;

            await g.UpdateFiles().ConfigureAwait(false);

            PostGit(author, committer, repo, workdirPath, cache, output);

            s.Stop();

            context.Logger.Info($"Operation Took {s.Elapsed}");
        }



        private static MarkdownDocument GenerateMarkdownDocument()
        {
            return MarkdownDocument.CreateBuilder()
                            .AddBlockParser<Blocks.HeaderBlock.HashParser>()
                            .AddBlockParser<Blocks.ListBlock.Parser>()
                            .AddBlockParser<Blocks.TableBlock.Parser>()
                            .AddBlockParser<Blocks.QuoteBlock.Parser>()
                            .AddBlockParser<Blocks.LinkReferenceBlock.Parser>()
                            .AddBlockParser<Markdown.Blocks.InsertBlock.Parser>()
                            .AddBlockParser<Markdown.Blocks.ChapterHeaderBlock.Parser>()
                            .AddBlockParser<Markdown.Blocks.YamlBlock<OrderMarkdownMetadata>.Parser>()
                            .AddBlockParser<Markdown.Blocks.YamlBlock<BookMetadata>.Parser>()

                            .AddInlineParser<Inlines.BoldTextInline.Parser>()
                            .AddInlineParser<Inlines.ItalicTextInline.Parser>()
                            .AddInlineParser<Inlines.EmojiInline.Parser>()
                            .AddInlineParser<Inlines.ImageInline.Parser>()
                            .AddInlineParser<Inlines.MarkdownLinkInline.Parser>()
                            .AddInlineParser<Inlines.StrikethroughTextInline.Parser>()
                            .AddInlineParser<Inlines.LinkAnchorInline.Parser>()

                            .Build();
        }

        private static LibGit2Sharp.Repository PreGit(GeneratorContext context, Config config, LibGit2Sharp.Signature author, string workdirPath, string cache, string output)
        {
            LibGit2Sharp.Repository repo;
            if (Directory.Exists(workdirPath))
            {
                repo = new LibGit2Sharp.Repository(workdirPath);

                var status = repo.RetrieveStatus(new LibGit2Sharp.StatusOptions() { });

                if (status.IsDirty)
                {
                    var currentCommit = repo.Head.Tip;
                    repo.Reset(LibGit2Sharp.ResetMode.Hard, currentCommit);
                }

                LibGit2Sharp.Commands.Pull(repo, author, new LibGit2Sharp.PullOptions() { MergeOptions = new LibGit2Sharp.MergeOptions() { FastForwardStrategy = LibGit2Sharp.FastForwardStrategy.FastForwardOnly } });

            }
            else
                repo = new LibGit2Sharp.Repository(LibGit2Sharp.Repository.Clone(config.WebsiteRepo ?? throw context.Exception($"{nameof(Config.SchemaRepo)} not set on configuration."), workdirPath));

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

        private class ContentVersions
        {
            private IEnumerable<string> enumerable;

            public ContentVersions(IEnumerable<string> enumerable)
            {
                this.enumerable = enumerable;
            }
        }
    }

    internal class Config
    {
        public string? ContentRepo { get; set; }
        public string? SchemaRepo { get; set; }
        public string? Layouts { get; set; }
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

        public string CalculatedVersion
        {
            get
            {
                string version;
                if (this.Type == GitRefType.Branch && this.Name == "master")
                    version = "vNext";
                else if (this.Type == GitRefType.Branch)
                    version = "draft/" + this.Name;
                else
                    version = this.Name;
                return version;
            }
        }

    }

    public class BookMetadata
    {
        public string Title { get; set; }
        public int Chapter { get; set; }
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
}
