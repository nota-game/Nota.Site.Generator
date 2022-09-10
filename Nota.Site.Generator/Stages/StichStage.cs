using AdaptMark.Parsers.Markdown;
using AdaptMark.Parsers.Markdown.Blocks;
using AdaptMark.Parsers.Markdown.Inlines;

using AngleSharp.Text;

using Nota.Site.Generator.Markdown.Blocks;

using Stasistium;
using Stasistium.Documents;
using Stasistium.Stages;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace Nota.Site.Generator.Stages
{
    public class StichStage : Stasistium.Stages.StageBase<MarkdownDocument, MarkdownDocument>
    {
        private const string NoChapterName = "index";
        private readonly int chapterSeperation;

        public StichStage(int chapterSeperation, IGeneratorContext context, string? name = null) : base(context, name)
        {
            this.chapterSeperation = chapterSeperation;
        }

        protected override async Task<ImmutableList<IDocument<MarkdownDocument>>> Work(ImmutableList<IDocument<MarkdownDocument>> input, OptionToken options)
        {

            ImmutableList<IDocument<MarkdownDocument>> performed = input;
            var list = ImmutableList<IDocument<MarkdownDocument>>.Empty.ToBuilder();

            var stateLookup = performed.ToDictionary(x => x.Id);
            var idToAfter = new Dictionary<string, string?>();


            var documentsWithChapters = new HashSet<string>();

            foreach (IDocument<MarkdownDocument> item in performed) {
                var resolver = new RelativePathResolver(item.Id, input.Select(x => x.Id));
                IDocument<MarkdownDocument> itemTask = item;
                OrderMarkdownMetadata? order = itemTask.Metadata.TryGetValue<OrderMarkdownMetadata>();
                if (order?.After != null) {
                    string? resolved = resolver[order.After];
                    if (resolved is not null) {
                        idToAfter[itemTask.Id] = resolved;
                    }
                } else {
                    idToAfter[itemTask.Id] = null;
                }

                if (ContainsChapters(itemTask.Value.Blocks)) {
                    _ = documentsWithChapters.Add(itemTask.Id);
                }
            }
            ILookup<string?, string> lookup = idToAfter.ToLookup(x => x.Value, x => x.Key);


            // check for wrong Pathes
            // var wrongPathes = idToAfter.Where(x => x.Value is null).Select(x => x.Key);
            // if (wrongPathes.Any()) {
            //     throw this.Context.Exception($"The after pathes of following documents could not be resolved: {string.Join(", ", wrongPathes)}");
            // }

            // check for double entrys

            // var problems = idToAfter
            //     .GroupBy(x => x.Value)
            //     .Where(x => x.Count() > 1)
            //     .Select(x => $"The documents {string.Join(", ", x.Select(y => y.Key))} all follow {x.Key}. Only one is allowed");

            // var problemString = string.Join("\n", problems);

            // if (problemString != string.Empty)
            //     throw this.Context.Exception("Ther were a problem with the ordering\n" + problemString);

            var orderedList = new List<string>();

            IEnumerable<string> startValues = idToAfter.Where(x => x.Value is null && idToAfter.ContainsValue(x.Key)).OrderBy(x => x.Key).Select(x => x.Key);
            // while (startValues.Any(x=> idToAfter.ContainsKey(x.Value)))
            // {

            // }
            // var startValues = idToAfter.Where(x => x.Value is null).OrderBy(x => x.Key).Select(x => x.Key);

            if (stateLookup.Count != 1) {
                // check for circles or gaps
                if (!startValues.Any()) {
                    // we have a circle.
                    string ordering = string.Join("\n", idToAfter.Select(x => $"{x.Key} => {x.Value}"));
                    throw Context.Exception("There is a  problem with the ordering. There Seems to be a circle dependency\n." + ordering);
                }




                var stack = new Stack<string>(startValues);

                while (stack.TryPop(out string? current)) {
                    orderedList.Add(current);
                    IEnumerable<string> next = lookup[current];
                    foreach (string? item in next.OrderByDescending(x => x)) {
                        stack.Push(item);
                    }
                }





                // if (startValues.Length > 1)
                //     throw this.Context.Exception($"There is a  problem with the ordering. There are Gaps. Possible gabs are after {string.Join(", ", startValues.Select(x => x.Key))}");

                // // Order the entrys.
                // {
                //     var current = startValues.Single().Key;
                //     while (true) {
                //         orderedList.Insert(0, current);
                //         if (!idToAfter.TryGetValue(current, out current!)) // if its null we break, and won't use it againg.
                //             break;
                //     }
                // }
            } else {
                orderedList.Add(stateLookup.First().Key);
            }


            await UpdateHeadersWithContaining(orderedList, stateLookup);

            // partition the list with chepters

            var chapterPartitions = new List<List<string>>();


            List<string>? currentChapterIds = null;

            foreach (string id in orderedList) {
                if (currentChapterIds is null) {
                    currentChapterIds = new List<string>();
                    chapterPartitions.Add(currentChapterIds);
                    currentChapterIds.Add(id);
                } else if (documentsWithChapters.Contains(id)) {
                    // since a document may not start with a new chapter but can contain text befor that from a
                    // previous chapter we add this Id also to the previous chapter.
                    currentChapterIds.Add(id);
                    currentChapterIds = new List<string>();
                    chapterPartitions.Add(currentChapterIds);
                    currentChapterIds.Add(id);
                } else {
                    currentChapterIds.Add(id);
                }
            }
            var newPatirions = new List<Partition>();
            for (int i = 0; i < chapterPartitions.Count; i++) {
                List<string> currentParition = chapterPartitions[i];
                bool takeFirstWithoutChapter = i == 0;
                bool takeLastChapter = i == chapterPartitions.Count - 1;

                IEnumerable<IDocument<MarkdownDocument>> documents = currentParition.Select(x => stateLookup[x]);

                List<List<(IDocument<MarkdownDocument> containingDocument, MarkdownBlock block)>> listOfChaptersInPartition = GetChaptersInPartitions(takeFirstWithoutChapter, takeLastChapter, documents);

                IDocument<MarkdownDocument>[] documentsInPartition = GetDocumentsInPartition(documents, listOfChaptersInPartition);
                var partitition = new Partition
                {
                    Ids = currentParition.ToArray(),
                    Documents = documentsInPartition.Select(x => (x.Id, x.Hash)).ToArray()
                };

                newPatirions.Add(partitition);
                foreach (IDocument<MarkdownDocument> item in documentsInPartition) {

                    list.Add(item);
                }


            }





            return list.ToImmutable();

        }

        private Task UpdateHeadersWithContaining(List<string> orderedList, Dictionary<string, IDocument<MarkdownDocument>> stateLookup)
        {
            var headerStack = new Stack<HeaderBlock>();
            foreach (MarkdownDocument? doc in orderedList.Select(x => stateLookup[x].Value)) {
                IList<MarkdownBlock> blocks = doc.Blocks;
                ScanAndReplace(blocks);
                void ScanAndReplace(IList<MarkdownBlock> blocks)
                {

                    for (int i = 0; i < blocks.Count; i++) {
                        MarkdownBlock block = blocks[i];
                        if (block is ChapterHeaderBlock ch && !string.IsNullOrWhiteSpace(ch.ChapterId)) {
                            while (headerStack.Count > 1 && headerStack.Peek().HeaderLevel >= ch.HeaderLevel) {
                                _ = headerStack.Pop();
                            }

                            headerStack.Push(ch);
                        } else if (block is HeaderBlock bl) {
                            while (headerStack.Count > 1 && headerStack.Peek().HeaderLevel >= bl.HeaderLevel) {
                                _ = headerStack.Pop();
                            }

                            headerStack.Push(bl);

                            string id = GenerateHeaderString(headerStack);
                            if (bl is ChapterHeaderBlock ch2) {
                                ch2.ChapterId = id;
                            } else {
                                var newChapter = new ChapterHeaderBlock()
                                {
                                    HeaderLevel = bl.HeaderLevel,
                                    Inlines = bl.Inlines,
                                    ChapterId = id
                                };
                                blocks[i] = newChapter;
                            }
                        } else if (block is SoureReferenceBlock sr) {
                            ScanAndReplace(sr.Blocks);
                        }
                    }
                }
            }
            return Task.CompletedTask;
        }

        public static string GenerateHeaderString(Stack<HeaderBlock> headerStack)
        {
            string id = string.Join("→", headerStack.Select(x => MarkdownInline.ToString(x.Inlines)).Reverse());
            id = id.Replace(' ', '-');
            return id;
        }

        private IDocument<MarkdownDocument>[] GetDocumentsInPartition(IEnumerable<IDocument<MarkdownDocument>> documents, List<List<(IDocument<MarkdownDocument> containingDocument, MarkdownBlock block)>> listOfChaptersInPartition)
        {
            return listOfChaptersInPartition.Select(x =>
            {
                var order = x.Select(value => value.containingDocument).Distinct().Select((value, index) => (value, index)).ToDictionary(y => y.value, y => y.index);
                IOrderedEnumerable<IGrouping<IDocument<MarkdownDocument>, (IDocument<MarkdownDocument> containingDocument, MarkdownBlock block)>> orderedGroups = x.GroupBy(y => y.containingDocument).OrderBy(y => order[y.Key]);

                IEnumerable<SoureReferenceBlock> referenceBlocks = orderedGroups.Select(group =>
                {
                    MarkdownBlock[] blocsk = group.Select(y => y.block).ToArray();
                    var wrapper = new SoureReferenceBlock(blocsk, group.Key);
                    return wrapper;
                });

                IDocument<MarkdownDocument> firstDocument = documents.First();
                MarkdownDocument newDoc = firstDocument.Value.GetBuilder().Build();
                newDoc.Blocks = referenceBlocks.ToArray();
                MarkdownBlock firstBlock = newDoc.Blocks.First();

                while (firstBlock is SoureReferenceBlock reference) {
                    firstBlock = reference.Blocks.First();
                }

                string chapterName;
                if (firstBlock is ChapterHeaderBlock chapterheaderBlock && chapterheaderBlock.ChapterId != null) {
                    chapterName = chapterheaderBlock.ChapterId;
                } else if (firstBlock is HeaderBlock headerBlock) {
                    chapterName = Stasistium.Stages.MarkdownRenderer.GetHeaderText(headerBlock);
                } else {
                    chapterName = StichStage.NoChapterName;
                }

                chapterName = System.IO.Path.GetInvalidFileNameChars().Aggregate(chapterName, (filename, invalidChar) => filename.Replace(invalidChar.ToString(), invalidChar.ToHex()), x => x);

                return firstDocument.With(newDoc, Context.GetHashForString(newDoc.ToString())).WithId(chapterName);
            }).ToArray();
        }

        private List<List<(IDocument<MarkdownDocument> containingDocument, MarkdownBlock block)>> GetChaptersInPartitions(bool takeFirstWithoutChapter, bool takeLastChapter, IEnumerable<IDocument<MarkdownDocument>> documents)
        {
            if (documents is not IList<IDocument<MarkdownDocument>> documentList) {
                documentList = documents.ToArray();
            }

            var listOfChaptersInPartition = new List<List<(IDocument<MarkdownDocument> containingDocument, MarkdownBlock block)>>();

            List<(IDocument<MarkdownDocument> containingDocument, MarkdownBlock block)>? currentList = null;

            if (takeFirstWithoutChapter) {
                currentList = new List<(IDocument<MarkdownDocument> containingDocument, MarkdownBlock block)>();
                listOfChaptersInPartition.Add(currentList);
            }

            for (int j = 0; j < documentList.Count; j++) {
                IDocument<MarkdownDocument> currentDocument = documentList[j];
                ScanBlocks(currentDocument.Value.Blocks);
                void ScanBlocks(IList<MarkdownBlock> blocks)
                {
                    foreach (MarkdownBlock block in blocks) {
                        if (block is SoureReferenceBlock soureReference) {
                            ScanBlocks(soureReference.Blocks);

                        } else if (currentList != null && !(block is HeaderBlock header_ && header_.HeaderLevel <= chapterSeperation)) {
                            currentList.Add((currentDocument, block));
                        } else if (block is HeaderBlock header && header.HeaderLevel <= chapterSeperation
                            && (j != documentList.Count - 1 // the last block is also the first of the nex partition
                                || takeLastChapter)) // we don't want to have (double) unless its the last partition.
                        {
                            currentList = new List<(IDocument<MarkdownDocument>, MarkdownBlock)>();
                            listOfChaptersInPartition.Add(currentList);
                            currentList.Add((currentDocument, block));
                        } else if (block is HeaderBlock header__ && header__.HeaderLevel <= chapterSeperation
                              && j == documentList.Count - 1) {
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
            return blocks.Any(x => x is HeaderBlock header && header.HeaderLevel <= chapterSeperation);
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
        public string[] Ids { get; set; } = Array.Empty<string>();
        /// <summary>
        /// Output ID and Hash of the documents.
        /// </summary>
        public (string Id, string Hash)[] Documents { get; set; } = Array.Empty<(string Id, string Hash)>();
    }

}
