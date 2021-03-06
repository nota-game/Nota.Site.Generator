﻿using AdaptMark.Parsers.Markdown;
using AdaptMark.Parsers.Markdown.Blocks;
using AdaptMark.Parsers.Markdown.Inlines;
using AngleSharp.Text;
using Nota.Site.Generator.Markdown.Blocks;
using Nota.Site.Generator.Stages;
using Stasistium;
using Stasistium.Core;
using Stasistium.Documents;
using Stasistium.Stages;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nota.Site.Generator.Stages
{
    public class StichStage<TItemCache, TCache> : Stasistium.Stages.MultiStageBase<MarkdownDocument, string, StichCache<TCache>>
        where TCache : class
        where TItemCache : class
    {
        private const string NoChapterName = "index";
        private readonly MultiStageBase<MarkdownDocument, TItemCache, TCache> input;
        private readonly int chapterSeperation;

        public StichStage(MultiStageBase<MarkdownDocument, TItemCache, TCache> input, int chapterSeperation, IGeneratorContext context, string? name = null) : base(context, name)
        {
            this.input = input ?? throw new ArgumentNullException(nameof(input));
            this.chapterSeperation = chapterSeperation;
        }

        protected override async Task<StageResultList<MarkdownDocument, string, StichCache<TCache>>> DoInternal(StichCache<TCache>? cache, OptionToken options)
        {
            var result = await this.input.DoIt(cache?.PreviousCache, options);

            var task = LazyTask.Create(async () =>
            {

                var performed = await result.Perform;
                var list = ImmutableList<StageResult<MarkdownDocument, string>>.Empty.ToBuilder();
                StichCache<TCache> newCache;

                if (cache is null)
                {
                    var stateLookup = performed.ToDictionary(x => x.Id);
                    var idToAfter = new Dictionary<string, string>();

                    var documentsWithChapters = new HashSet<string>();

                    foreach (var item in performed)
                    {
                        var resolver = new RelativePathResolver(item.Id, result.Ids);
                        var itemTask = await item.Perform;
                        var order = itemTask.Metadata.TryGetValue<OrderMarkdownMetadata>();
                        if (order?.After != null)
                        {
                            var resolved = resolver[order.After];
                            idToAfter[itemTask.Id] = resolved;
                        }

                        if (this.ContainsChapters(itemTask.Value.Blocks))
                            documentsWithChapters.Add(itemTask.Id);

                    }


                    // check for wrong Pathes
                    var wrongPathes = idToAfter.Where(x => x.Value is null).Select(x => x.Key);
                    if (wrongPathes.Any())
                    {
                        throw this.Context.Exception($"The after pathes of following documents could not be resolved: {string.Join(", ", wrongPathes)}");
                    }

                    // check for double entrys

                    var problems = idToAfter
                        .GroupBy(x => x.Value)
                        .Where(x => x.Count() > 1)
                        .Select(x => $"The documents { string.Join(", ", x.Select(y => y.Key)) } all follow {x.Key}. Only one is allowed");

                    var problemString = string.Join("\n", problems);

                    if (problemString != string.Empty)
                        throw this.Context.Exception("Ther were a problem with the ordering\n" + problemString);

                    var orderedList = new List<string>();

                    var startValues = idToAfter.Where(x => !idToAfter.ContainsValue(x.Key)).ToArray();

                    if (stateLookup.Count != 1)
                    {
                        // check for circles or gaps
                        if (startValues.Length == 0)
                            // we have a circle.
                            throw this.Context.Exception("There is a  problem with the ordering. There Seems to be a circle dependency.");
                        if (startValues.Length > 1)
                            throw this.Context.Exception($"There is a  problem with the ordering. There are Gaps. Possible gabs are after {string.Join(", ", startValues.Select(x => x.Key))}");

                        // Order the entrys.
                        {
                            var current = startValues.Single().Key;
                            while (true)
                            {
                                orderedList.Insert(0, current);
                                if (!idToAfter.TryGetValue(current, out current!)) // if its null we break, and won't use it againg.
                                    break;
                            }
                        }
                    }
                    else
                    {
                        orderedList.Add(stateLookup.First().Key);
                    }

                    await this.UpdateHeadersWithContaining(orderedList, stateLookup);

                    // partition the list with chepters

                    var chapterPartitions = new List<List<string>>();


                    List<string>? currentChapterIds = null;

                    foreach (var id in orderedList)
                    {
                        if (currentChapterIds is null)
                        {
                            currentChapterIds = new List<string>();
                            chapterPartitions.Add(currentChapterIds);
                            currentChapterIds.Add(id);
                        }
                        else if (documentsWithChapters.Contains(id))
                        {
                            // since a document may not start with a new chapter but can contain text befor that from a
                            // previous chapter we add this Id also to the previous chapter.
                            currentChapterIds.Add(id);
                            currentChapterIds = new List<string>();
                            chapterPartitions.Add(currentChapterIds);
                            currentChapterIds.Add(id);
                        }
                        else
                        {
                            currentChapterIds.Add(id);
                        }
                    }
                    var newPatirions = new List<Partition>();
                    for (int i = 0; i < chapterPartitions.Count; i++)
                    {
                        var currentParition = chapterPartitions[i];
                        var takeFirstWithoutChapter = i == 0;
                        var takeLastChapter = i == chapterPartitions.Count - 1;

                        var documents = await Task.WhenAll(currentParition.Select(x => stateLookup[x].Perform.AsTask())).ConfigureAwait(false);

                        var listOfChaptersInPartition = this.GetChaptersInPartitions(takeFirstWithoutChapter, takeLastChapter, documents);

                        var documentsInPartition = this.GetDocumentsInPartition(documents, listOfChaptersInPartition);
                        var partitition = new Partition
                        {
                            Ids = currentParition.ToArray(),
                            Documents = documentsInPartition.Select(x => (x.Id, x.Hash)).ToArray()
                        };

                        newPatirions.Add(partitition);
                        foreach (var item in documentsInPartition)
                        {

                            list.Add(StageResult.CreateStageResult(this.Context, item, true, item.Id, item.Hash, item.Hash));
                        }


                    }

                    newCache = new StichCache<TCache>()
                    {
                        DocumentsWithChapters = documentsWithChapters.ToArray(),
                        IDToAfterEntry = idToAfter,
                        Partitions = newPatirions.ToArray(),
                        PreviousCache = result.Cache,
                        Hash = this.Context.GetHashForObject(list.Select(x => x.Hash)),
                    };
                }
                else
                {
                    var stateLookup = performed.ToDictionary(x => x.Id);
                    var idToAfter = cache.IDToAfterEntry.ToDictionary(x => x.Key, x => x.Value);

                    var documentsWithChapters = new HashSet<string>(cache.DocumentsWithChapters);

                    foreach (var item in performed)
                    {
                        if (item.HasChanges)
                        {
                            var resolver = new RelativePathResolver(item.Id, result.Ids);
                            var itemTask = await item.Perform;
                            var order = itemTask.Metadata.TryGetValue<OrderMarkdownMetadata>();
                            if (order?.After != null)
                                idToAfter[itemTask.Id] = resolver[order.After];
                            else
                                idToAfter.Remove(itemTask.Id);

                            if (this.ContainsChapters(itemTask.Value.Blocks))
                                documentsWithChapters.Add(itemTask.Id);
                            else
                                documentsWithChapters.Remove(itemTask.Id);

                        }
                    }

                    // check for double entrys

                    var problems = idToAfter
                        .GroupBy(x => x.Value)
                        .Where(x => x.Count() > 1)
                        .Select(x => $"The documents { string.Join(", ", x.Select(y => y.Key)) } all follow {x.Key}. Only one is allowed");

                    var problemString = string.Join("\n", problems);

                    if (problemString != string.Empty)
                        throw this.Context.Exception("Ther were a problem with the ordering\n" + problemString);

                    var orderedList = new List<string>();

                    if (stateLookup.Count != 1)
                    {
                        var startValues = idToAfter.Where(x => !idToAfter.ContainsValue(x.Key)).ToArray();

                        // check for circles or gaps
                        if (startValues.Length == 0)
                            // we have a circle.
                            throw this.Context.Exception("There is a  problem with the ordering. There Seems to be a circle dependency.");
                        if (startValues.Length > 1)
                            throw this.Context.Exception($"There is a  problem with the ordering. There are Gaps. Possible gabs are after {string.Join(", ", startValues.Select(x => x.Key))}");

                        // Order the entrys.
                        {
                            var current = startValues.Single().Key;
                            while (true)
                            {
                                orderedList.Insert(0, current);
                                if (!idToAfter.TryGetValue(current, out current!)) // if its null we break, and won't use it againg.
                                    break;
                            }
                        }
                    }
                    else
                    {
                        orderedList.Add(stateLookup.First().Key);
                    }

                    // partition the list with chepters

                    var chapterPartitions = new List<List<string>>();

                    //TODO: find a way to only resolve the change documents
                    // I need propably to cache the last header, its level and the parent header
                    // of each document. In additon the replacement of Header with ChapterHader 
                    // needs to be performed lazy when a document is requested later.
                    // This looks like so much trouble...
                    await this.UpdateHeadersWithContaining(orderedList, stateLookup);


                    List<string>? currentChapterIds = null;

                    foreach (var id in orderedList)
                    {
                        if (currentChapterIds is null)
                        {
                            currentChapterIds = new List<string>();
                            chapterPartitions.Add(currentChapterIds);
                            currentChapterIds.Add(id);
                        }
                        else if (documentsWithChapters.Contains(id))
                        {
                            // since a document may not start with a new chapter but can contain text befor that from a
                            // previous chapter we add this Id also to the previous chapter.
                            currentChapterIds.Add(id);
                            currentChapterIds = new List<string>();
                            chapterPartitions.Add(currentChapterIds);
                            currentChapterIds.Add(id);
                        }
                        else
                        {
                            currentChapterIds.Add(id);
                        }
                    }
                    var newPatirions = new List<Partition>();
                    for (int i = 0; i < chapterPartitions.Count; i++)
                    {
                        var currentParition = chapterPartitions[i];
                        var takeFirstWithoutChapter = i == 0;
                        var takeLastChapter = i == chapterPartitions.Count - 1;
                        var cachePartition = cache.Partitions.FirstOrDefault(x => x.Ids.FirstOrDefault() == currentParition.First());
                        if (!(cachePartition?.Ids.SequenceEqual(currentParition) ?? false)
                            || currentParition.Select(x => stateLookup[x].HasChanges).Any())
                        {
                            var documents = await Task.WhenAll(currentParition.Select(x => stateLookup[x].Perform.AsTask())).ConfigureAwait(false);

                            var listOfChaptersInPartition = this.GetChaptersInPartitions(takeFirstWithoutChapter, takeLastChapter, documents);

                            var documentsInPartition = this.GetDocumentsInPartition(documents, listOfChaptersInPartition);


                            var partitition = new Partition
                            {
                                Ids = currentParition.ToArray(),
                                Documents = documentsInPartition.Select(x => (x.Id, x.Hash)).ToArray()
                            };

                            newPatirions.Add(partitition);
                            foreach (var item in documentsInPartition)
                            {
                                var oldHash = cachePartition?.Documents.FirstOrDefault(x => x.Id == item.Id).Hash;
                                list.Add(StageResult.CreateStageResult(this.Context, item, oldHash != item.Hash, item.Id, item.Hash, item.Hash));
                            }
                        }
                        else
                        {
                            newPatirions.Add(cachePartition);

                            var baseTask = LazyTask.Create(async () =>
                            {
                                var documents = await Task.WhenAll(currentParition.Select(x => stateLookup[x].Perform.AsTask())).ConfigureAwait(false);

                                var listOfChaptersInPartition = this.GetChaptersInPartitions(takeFirstWithoutChapter, takeLastChapter, documents);
                                var documentsInPartition = this.GetDocumentsInPartition(documents, listOfChaptersInPartition);

                                return documentsInPartition;
                            });

                            foreach (var (oldId, oldHash) in cachePartition.Documents)
                            {
                                var task = LazyTask.Create(async () =>
                                {
                                    var x = await baseTask;
                                    return x.First(x => x.Id == oldId && x.Hash == oldHash);
                                });
                                list.Add(StageResult.CreateStageResult(this.Context, task, false, oldId, oldHash, oldHash));
                            }

                        }

                    }

                    newCache = new StichCache<TCache>()
                    {
                        DocumentsWithChapters = documentsWithChapters.ToArray(),
                        IDToAfterEntry = idToAfter,
                        Partitions = newPatirions.ToArray(),
                        PreviousCache = result.Cache,
                        Hash = this.Context.GetHashForObject(list.Select(x => x.Hash)),
                    };

                }






                return (list.ToImmutable(), newCache);
            });

            ImmutableList<string> ids;
            var hasChanges = false;

            if (cache is null || result.HasChanges)
            {
                var (performed, newCache) = await task;
                ids = performed.Select(x => x.Id).ToImmutableList();
                if (cache is null)
                {
                    hasChanges = true;
                }
                else
                {
                    hasChanges = !cache.Partitions.SelectMany(x => x.Documents).Select(x => x.Id).SequenceEqual(ids)
                        || performed.Any(x => x.HasChanges);
                }

                return this.Context.CreateStageResultList(performed, hasChanges, ids, newCache, newCache.Hash, result.Cache);
            }
            else
                ids = cache.Partitions.SelectMany(x => x.Documents).Select(x => x.Id).ToImmutableList();
            var actualTask = LazyTask.Create(async () =>
            {
                var temp = await task;
                return temp.Item1;
            });
            return this.Context.CreateStageResultList(actualTask, hasChanges, ids, cache, cache.Hash, result.Cache);
        }

        private async Task UpdateHeadersWithContaining(List<string> orderedList, Dictionary<string, StageResult<MarkdownDocument, TItemCache>> stateLookup)
        {
            var headerStack = new Stack<HeaderBlock>();
            foreach (var item in orderedList.Select(async x => (await stateLookup[x].Perform).Value))
            {
                var doc = await item;
                var blocks = doc.Blocks;
                ScanAndReplace(blocks);
                void ScanAndReplace(IList<MarkdownBlock> blocks)
                {

                    for (var i = 0; i < blocks.Count; i++)
                    {
                        var block = blocks[i];
                        if (block is ChapterHeaderBlock ch && !string.IsNullOrWhiteSpace(ch.ChapterId))
                        {
                            while (headerStack.Count > 1 && headerStack.Peek().HeaderLevel >= ch.HeaderLevel)
                                headerStack.Pop();
                            headerStack.Push(ch);
                        }
                        else if (block is HeaderBlock bl)
                        {
                            while (headerStack.Count > 1 && headerStack.Peek().HeaderLevel >= bl.HeaderLevel)
                                headerStack.Pop();
                            headerStack.Push(bl);

                            var id = GenerateHeaderString(headerStack);
                            if (bl is ChapterHeaderBlock ch2)
                                ch2.ChapterId = id;
                            else
                            {
                                var newChapter = new ChapterHeaderBlock()
                                {
                                    HeaderLevel = bl.HeaderLevel,
                                    Inlines = bl.Inlines,
                                    ChapterId = id
                                };
                                blocks[i] = newChapter;
                            }
                        }
                        else if (block is SoureReferenceBlock sr)
                        {
                            ScanAndReplace(sr.Blocks);
                        }
                    }
                }
            }
        }

        public static string GenerateHeaderString(Stack<HeaderBlock> headerStack)
        {
            var id = string.Join("→", headerStack.Select(x => MarkdownInline.ToString(x.Inlines)).Reverse());
            id = id.Replace(' ', '-');
            return id;
        }

        private IDocument<MarkdownDocument>[] GetDocumentsInPartition(IDocument<MarkdownDocument>[] documents, List<List<(IDocument<MarkdownDocument> containingDocument, MarkdownBlock block)>> listOfChaptersInPartition)
        {
            return listOfChaptersInPartition.Select(x =>
            {
                var order = x.Select(value => value.containingDocument).Distinct().Select((value, index) => (value, index)).ToDictionary(y => y.value, y => y.index);
                var orderedGroups = x.GroupBy(y => y.containingDocument).OrderBy(y => order[y.Key]);

                var referenceBlocks = orderedGroups.Select(group =>
                {
                    var blocsk = group.Select(y => y.block).ToArray();
                    var wrapper = new SoureReferenceBlock(blocsk, group.Key);
                    return wrapper;
                });

                var newDoc = documents.First().Value.GetBuilder().Build();
                newDoc.Blocks = referenceBlocks.ToArray();
                var firstBlock = newDoc.Blocks.First();

                while (firstBlock is SoureReferenceBlock reference)
                    firstBlock = reference.Blocks.First();

                string chapterName;
                if (firstBlock is ChapterHeaderBlock chapterheaderBlock && chapterheaderBlock.ChapterId != null)
                    chapterName = chapterheaderBlock.ChapterId;
                else if (firstBlock is HeaderBlock headerBlock)
                    chapterName = Stasistium.Stages.MarkdownRenderer.GetHeaderText(headerBlock);
                else
                    chapterName = StichStage<TItemCache, TCache>.NoChapterName;

                chapterName = System.IO.Path.GetInvalidFileNameChars().Aggregate(chapterName, (filename, invalidChar) => filename.Replace(invalidChar.ToString(), invalidChar.ToHex()), x => x);

                return documents.First().With(newDoc, this.Context.GetHashForString(newDoc.ToString())).WithId(chapterName);
            }).ToArray();
        }

        private List<List<(IDocument<MarkdownDocument> containingDocument, MarkdownBlock block)>> GetChaptersInPartitions(bool takeFirstWithoutChapter, bool takeLastChapter, IDocument<MarkdownDocument>[] documents)
        {
            var listOfChaptersInPartition = new List<List<(IDocument<MarkdownDocument> containingDocument, MarkdownBlock block)>>();

            List<(IDocument<MarkdownDocument> containingDocument, MarkdownBlock block)>? currentList = null;

            if (takeFirstWithoutChapter)
            {
                currentList = new List<(IDocument<MarkdownDocument> containingDocument, MarkdownBlock block)>();
                listOfChaptersInPartition.Add(currentList);
            }

            for (int j = 0; j < documents.Length; j++)
            {
                var currentDocument = documents[j];
                ScanBlocks(currentDocument.Value.Blocks);
                void ScanBlocks(IList<MarkdownBlock> blocks)
                {
                    foreach (var block in blocks)
                    {
                        if (block is SoureReferenceBlock soureReference)
                        {
                            ScanBlocks(soureReference.Blocks);

                        }
                        else if (currentList != null && !(block is HeaderBlock header_ && header_.HeaderLevel <= this.chapterSeperation))
                            currentList.Add((currentDocument, block));

                        else if (block is HeaderBlock header && header.HeaderLevel <= this.chapterSeperation
                            && (j != documents.Length - 1 // the last block is also the first of the nex partition
                                || takeLastChapter)) // we don't want to have (double) unless its the last partition.
                        {
                            currentList = new List<(IDocument<MarkdownDocument>, MarkdownBlock)>();
                            listOfChaptersInPartition.Add(currentList);
                            currentList.Add((currentDocument, block));
                        }
                        else if (block is HeaderBlock header__ && header__.HeaderLevel <= this.chapterSeperation
                            && j == documents.Length - 1)
                        {
                            // we wan't to break the for not foreach, but we are alreary in the last for loop.
                            break; // so this is enough
                        }
                    }

                }
            }


            return listOfChaptersInPartition;
        }


        private bool ContainsChapters(IList<MarkdownBlock> blocks)
        {
            return blocks.Any(x => x is HeaderBlock header && header.HeaderLevel <= this.chapterSeperation);
        }
    }

    internal class ChapterMetadata
    {

    }

    internal class OrderMarkdownMetadata
    {
        public string? After { get; set; }
    }

    public class Partition
    {
        /// <summary>
        /// Input ids in this partition
        /// </summary>
        public string[] Ids { get; set; }
        /// <summary>
        /// Output ID and Hash of the documents.
        /// </summary>
        public (string Id, string Hash)[] Documents { get; set; }
    }

    public class StichCache<TCache> : IHavePreviousCache<TCache>
        where TCache : class
    {
        public TCache PreviousCache { get; set; }

        public Dictionary<string, string> IDToAfterEntry { get; set; }

        public string[] DocumentsWithChapters { get; set; }

        public Partition[] Partitions { get; set; }
        public string Hash { get; set; }
    }

}

namespace Nota.Site.Generator
{
    public static partial class StageExtensions
    {
        public static StichStage<TListItemCache, TListCache> Stich<TListItemCache, TListCache>(this MultiStageBase<MarkdownDocument, TListItemCache, TListCache> input, int chapterSeperation, string? name = null)
        where TListItemCache : class
        where TListCache : class
        {
            return new StichStage<TListItemCache, TListCache>(input, chapterSeperation, input.Context, name);
        }
    }
}