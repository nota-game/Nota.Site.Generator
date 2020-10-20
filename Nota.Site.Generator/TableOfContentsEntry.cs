using System.Collections.Generic;

namespace Nota.Site.Generator
{
    public class TableOfContentsEntry
    {
        public TableOfContentsEntry()
        {
        }

        public string Page { get; internal set; }
        public string Id { get; internal set; }
        public string Title { get; internal set; }
        public int Level { get; internal set; }
        public List<TableOfContentsEntry> Sections { get; } = new List<TableOfContentsEntry>();
    }
}