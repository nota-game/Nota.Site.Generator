using Microsoft.Toolkit.Parsers.Markdown;
using Microsoft.Toolkit.Parsers.Markdown.Blocks;
using Microsoft.Toolkit.Parsers.Markdown.Inlines;
using Nota.Site.Generator.Markdown.Blocks;
using Stasistium;
using Stasistium.Core;
using Stasistium.Documents;
using Stasistium.Stages;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nota.Site.Generator
{
    public class StichStage<TItemCache, TCache> : Stasistium.Stages.MultiStageBase<MarkdownDocument, string, StichCache<TCache>>
        where TCache : class
        where TItemCache : class
    {
        private readonly StagePerformHandler<MarkdownDocument, TItemCache, TCache> input;

        public StichStage(StagePerformHandler<MarkdownDocument, TItemCache, TCache> input, IGeneratorContext context, string? name = null) : base(context, name)
        {
            this.input = input ?? throw new ArgumentNullException(nameof(input));
        }

        protected override async Task<StageResultList<MarkdownDocument, string, StichCache<TCache>>> DoInternal(StichCache<TCache>? cache, OptionToken options)
        {
            var result = await this.input(cache?.PreviousCache, options);

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
                            idToAfter[itemTask.Id] = resolver[order.After];

                        if (this.ContainsChapters(itemTask.Value.Blocks))
                            documentsWithChapters.Add(itemTask.Id);

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

                        var listOfChaptersInPartition = new List<List<MarkdownBlock>>();

                        List<MarkdownBlock>? currentList = null;

                        if (takeFirstWithoutChapter)
                        {
                            currentList = new List<MarkdownBlock>();
                            listOfChaptersInPartition.Add(currentList);
                        }

                        for (int j = 0; j < documents.Length; j++)
                        {
                            foreach (var block in documents[j].Value.Blocks)
                            {
                                if (currentList != null && !(block is HeaderBlock header_ && header_.HeaderLevel == 1))
                                    currentList.Add(block);

                                else if (block is HeaderBlock header && header.HeaderLevel == 1
                                    && (j != documents.Length - 1 // the last block is also the first of the nex partition
                                        || takeLastChapter)) // we don't want to have (double) unless its the last partition.
                                {
                                    currentList = new List<MarkdownBlock>();
                                    listOfChaptersInPartition.Add(currentList);
                                    currentList.Add(block);
                                }
                                else if (block is HeaderBlock header__ && header__.HeaderLevel == 1
                                    && j == documents.Length - 1)
                                {
                                    // we wan't to break the for not foreach, but we are alreary in the last for loop.
                                    break; // so this is enough
                                }
                            }

                        }

                        var partitition = new Partition();

                        var documentsInPartition = listOfChaptersInPartition.Select(x =>
                        {
                            var newDoc = documents.First().Value.GetBuilder().Build();
                            newDoc.Blocks = x.ToArray();

                            string chapterName;
                            if (newDoc.Blocks.First() is ChapterHeaderBlock chapterheaderBlock && chapterheaderBlock.ChapterId != null)
                                chapterName = chapterheaderBlock.ChapterId;
                            if (newDoc.Blocks.First() is HeaderBlock headerBlock)
                                chapterName = Stasistium.Stages.MarkdownRenderer.GetHeaderText(headerBlock);
                            else
                                chapterName = "Pre";
                            return documents.First().With(newDoc, this.Context.GetHashForString(newDoc.ToString())).WithId(chapterName);
                        }).ToArray();
                        partitition.Ids = currentParition.ToArray();

                        partitition.Documents = documentsInPartition.Select(x => (x.Id, x.Hash)).ToArray();

                        newPatirions.Add(partitition);
                        foreach (var item in documentsInPartition)
                        {

                            list.Add(StageResult.Create(item, true, item.Id, item.Hash));
                        }


                    }

                    newCache = new StichCache<TCache>()
                    {
                        DocumentsWithChapters = documentsWithChapters.ToArray(),
                        IDToAfterEntry = idToAfter,
                        Partitions = newPatirions.ToArray(),
                        PreviousCache = result.Cache
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
                            var order = itemTask.Metadata.GetValue<OrderMarkdownMetadata>();
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

                            var listOfChaptersInPartition = new List<List<MarkdownBlock>>();

                            List<MarkdownBlock>? currentList = null;

                            if (takeFirstWithoutChapter)
                            {
                                currentList = new List<MarkdownBlock>();
                                listOfChaptersInPartition.Add(currentList);
                            }

                            for (int j = 0; j < documents.Length; j++)
                            {
                                foreach (var block in documents[j].Value.Blocks)
                                {
                                    if (currentList != null && !(block is HeaderBlock header_ && header_.HeaderLevel == 1))
                                        currentList.Add(block);

                                    else if (block is HeaderBlock header && header.HeaderLevel == 1
                                        && (j != documents.Length - 1 // the last block is also the first of the nex partition
                                            || takeLastChapter)) // we don't want to have (double) unless its the last partition.
                                    {
                                        currentList = new List<MarkdownBlock>();
                                        listOfChaptersInPartition.Add(currentList);
                                        currentList.Add(block);
                                    }
                                    else if (block is HeaderBlock header__ && header__.HeaderLevel == 1
                                        && j == documents.Length - 1)
                                    {
                                        // we wan't to break the for not foreach, but we are alreary in the last for loop.
                                        break; // so this is enough
                                    }
                                }

                            }

                            var partitition = new Partition();
                            partitition.Ids = currentParition.ToArray();

                            var documentsInPartition = listOfChaptersInPartition.Select(x =>
                            {
                                var newDoc = documents.First().Value.GetBuilder().Build();
                                newDoc.Blocks = x.ToArray();

                                string chapterName;
                                if (newDoc.Blocks.First() is ChapterHeaderBlock chapterheaderBlock && chapterheaderBlock.ChapterId != null)
                                    chapterName = chapterheaderBlock.ChapterId;
                                if (newDoc.Blocks.First() is HeaderBlock headerBlock)
                                    chapterName = Stasistium.Stages.MarkdownRenderer.GetHeaderText(headerBlock);
                                else
                                    chapterName = "Pre";
                                return documents.First().With(newDoc, this.Context.GetHashForString(newDoc.ToString())).WithId(chapterName);
                            }).ToArray();

                            partitition.Documents = documentsInPartition.Select(x => (x.Id, x.Hash)).ToArray();

                            newPatirions.Add(partitition);
                            foreach (var item in documentsInPartition)
                            {
                                var oldHash = cachePartition?.Documents.FirstOrDefault(x => x.Id == item.Id).Hash;
                                list.Add(StageResult.Create(item, oldHash != item.Hash, item.Id, item.Hash));
                            }
                        }
                        else
                        {
                            newPatirions.Add(cachePartition);

                            var baseTask = LazyTask.Create(async () =>
                            {
                                var documents = await Task.WhenAll(currentParition.Select(x => stateLookup[x].Perform.AsTask())).ConfigureAwait(false);

                                var listOfChaptersInPartition = new List<List<MarkdownBlock>>();

                                List<MarkdownBlock>? currentList = null;

                                if (takeFirstWithoutChapter)
                                {
                                    currentList = new List<MarkdownBlock>();
                                    listOfChaptersInPartition.Add(currentList);
                                }

                                for (int j = 0; j < documents.Length; j++)
                                {
                                    foreach (var block in documents[j].Value.Blocks)
                                    {
                                        if (currentList != null && !(block is HeaderBlock header_ && header_.HeaderLevel == 1))
                                            currentList.Add(block);

                                        else if (block is HeaderBlock header && header.HeaderLevel == 1
                                            && (j != documents.Length - 1 // the last block is also the first of the nex partition
                                                || takeLastChapter)) // we don't want to have (double) unless its the last partition.
                                        {
                                            currentList = new List<MarkdownBlock>();
                                            listOfChaptersInPartition.Add(currentList);
                                            currentList.Add(block);
                                        }
                                        else if (block is HeaderBlock header__ && header__.HeaderLevel == 1
                                            && j == documents.Length - 1)
                                        {
                                            // we wan't to break the for not foreach, but we are alreary in the last for loop.
                                            break; // so this is enough
                                        }

                                    }

                                }


                                var documentsInPartition = listOfChaptersInPartition.Select(x =>
                                {
                                    var newDoc = documents.First().Value.GetBuilder().Build();
                                    newDoc.Blocks = x.ToArray();

                                    string chapterName;
                                    if (newDoc.Blocks.First() is ChapterHeaderBlock chapterheaderBlock && chapterheaderBlock.ChapterId != null)
                                        chapterName = chapterheaderBlock.ChapterId;
                                    if (newDoc.Blocks.First() is HeaderBlock headerBlock)
                                        chapterName = Stasistium.Stages.MarkdownRenderer.GetHeaderText(headerBlock);
                                    else
                                        chapterName = "Pre";
                                    return this.Context.Create(newDoc, this.Context.GetHashForString(newDoc.ToString()), chapterName);
                                }).ToArray();

                                return documentsInPartition;
                            });

                            foreach (var (oldId, oldHash) in cachePartition.Documents)
                            {
                                var task = LazyTask.Create(async () =>
                                {
                                    var x = await baseTask;
                                    return x.First(x => x.Id == oldId && x.Hash == oldHash);
                                });
                                list.Add(StageResult.Create(task, false, oldId, oldHash));
                            }

                        }

                    }

                    newCache = new StichCache<TCache>()
                    {
                        DocumentsWithChapters = documentsWithChapters.ToArray(),
                        IDToAfterEntry = idToAfter,
                        Partitions = newPatirions.ToArray(),
                        PreviousCache = result.Cache
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

                return StageResultList.Create(performed, hasChanges, ids, newCache);
            }
            else
                ids = cache.Partitions.SelectMany(x => x.Documents).Select(x => x.Id).ToImmutableList();
            var actualTask = LazyTask.Create(async () =>
            {
                var temp = await task;
                return temp.Item1;
            });
            return StageResultList.Create(actualTask, hasChanges, ids, cache);
        }



        private bool ContainsChapters(IList<MarkdownBlock> blocks)
        {
            return blocks.Any(x => x is HeaderBlock header && header.HeaderLevel == 1);
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

    public class StichCache<TCache>
    {
        public TCache PreviousCache { get; set; }

        public Dictionary<string, string> IDToAfterEntry { get; set; }

        public string[] DocumentsWithChapters { get; set; }

        public Partition[] Partitions { get; set; }
    }
}

namespace Nota.Site.Generator
{
    public static partial class StageExtensions
    {
        public static StichStage<TListItemCache, TListCache> Stich<TListItemCache, TListCache>(this MultiStageBase<MarkdownDocument, TListItemCache, TListCache> input, string? name = null)
        where TListItemCache : class
        where TListCache : class
        {
            return new StichStage<TListItemCache, TListCache>(input.DoIt, input.Context, name);
        }
    }
}