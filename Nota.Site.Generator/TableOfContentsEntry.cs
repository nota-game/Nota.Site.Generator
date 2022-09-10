using System.Collections.Generic;

namespace Nota.Site.Generator
{
    public class TableOfContentsEntry
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public TableOfContentsEntry()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
        }

        public string Page { get; internal set; }
        public string Id { get; internal set; }
        public string Title { get; internal set; }
        public int Level { get; internal set; }
        public List<TableOfContentsEntry> Sections { get; } = new List<TableOfContentsEntry>();
    }
}