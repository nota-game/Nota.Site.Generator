﻿using Stasistium;
using Stasistium.Documents;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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

            using var context = new GeneratorContext();

            var configFile = context.StageFromResult(configuration.FullName, x => x)
             .File()
             .Json()
             .For<Config>();

            var config = (await (await configFile.DoIt(null, new GenerationOptions().Token)).Perform).result.Value;

            // Create the committer's signature and commit
            var author = new LibGit2Sharp.Signature("NotaSiteGenerator", "@NotaSiteGenerator", DateTime.Now);
            var committer = author;


            LibGit2Sharp.Repository repo;
            const string workdirPath = "gitOut";
            const string cache = "cache";
            const string output = "out";
            if (Directory.Exists(workdirPath))
            {
                repo = new LibGit2Sharp.Repository(workdirPath);
                LibGit2Sharp.Commands.Pull(repo, author, new LibGit2Sharp.PullOptions() { MergeOptions = new LibGit2Sharp.MergeOptions() { FastForwardStrategy = LibGit2Sharp.FastForwardStrategy.FastForwardOnly } });
            }
            else
                repo = new LibGit2Sharp.Repository(LibGit2Sharp.Repository.Clone(config.WebsiteRepo ?? throw context.Exception($"{nameof(Config.SchemaRepo)} not set on configuration."), workdirPath));
            using var repo2 = repo;


            if (Directory.Exists(output))
                Directory.Delete(output, true);

            if (Directory.Exists(cache))
                Directory.Delete(cache, true);

            Directory.CreateDirectory(output);
            foreach (var path in new DirectoryInfo(workdirPath).GetDirectories().Where(x => x.Name != ".git"))
            {
                if (path.Name == "cache")
                    path.MoveTo(cache);
                else
                    path.MoveTo(Path.Combine(output, path.Name));
            }



            var contentRepo = configFile.Transform(x => x.With(x.Value.ContentRepo ?? throw x.Context.Exception($"{nameof(Config.ContentRepo)} not set on configuration."), x.Value.ContentRepo))
                .GitModul();

            var schemaRepo = configFile.Transform(x => x.With(x.Value.SchemaRepo ?? throw x.Context.Exception($"{nameof(Config.SchemaRepo)} not set on configuration."), x.Value.SchemaRepo).With(x.Metadata.Add(new HostMetadata() { Host = x.Value.Host })))
                .GitModul();

            var layoutProvider = configFile.Transform(x => x.With(x.Value.Layouts ?? "layout", x.Value.Layouts ?? "layout")).FileSystem().FileProvider("Layout");

            var generatorOptions = new GenerationOptions()
            {
                CompressCache = false,
                Refresh = true
            };
            var s = System.Diagnostics.Stopwatch.StartNew();
            var files = contentRepo
                .SelectMany(input =>
                    input
                    .Transform(x => x.With(x.Metadata.Add(new GitMetadata(x.Value.FrindlyName, x.Value.Type))))
                    .GitRefToFiles()
                    .Sidecar()
                        .For<BookMetadata>(".metadata")
                    .Where(x => System.IO.Path.GetExtension(x.Id) == ".md")
                    .Select(x => x.Markdown().MarkdownToHtml().TextToStream())
                    .Transform(x => x.WithId(Path.Combine(x.Metadata.GetValue<GitMetadata>()!.CalculatedVersion, x.Id)))
                );


            var contentVersions = contentRepo.Transform(input =>
            {
                var gitData = new GitMetadata( input.Value);
                string version = gitData.CalculatedVersion;
                return input.With(version, version).WithId(version);
            })
            .OrderBy(x => new VersionComparer(x.Id));

            var razorProvider = files
                .FileProvider("Content")
                .Concat(layoutProvider)
                .RazorProvider("Content", "Layout/ViewStart.cshtml");

            var rendered = files.Select(x => x.Razor(razorProvider).TextToStream());
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
                     var version  = gitData.CalculatedVersion;

                     return x.WithId($"schema/{version}/{x.Id.TrimStart('/')}");
                 })

                        );

            var g = rendered
                .Transform(x => x.WithId(Path.ChangeExtension(x.Id, ".html")))
                .Concat(schemaFiles)
                .Persist(new DirectoryInfo(output), generatorOptions)
                ;

            await g.UpdateFiles().ConfigureAwait(false);

            foreach (var path in new DirectoryInfo(output).GetDirectories())
            {
                path.MoveTo(Path.Combine(workdirPath, path.Name));
            }
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

            s.Stop();

            context.Logger.Info($"Operation Took {s.Elapsed}");




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

    internal class BookMetadata
    {
        public string? Title { get; set; }
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
