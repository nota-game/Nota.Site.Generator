using System.Collections.Generic;

namespace Nota.Site.Generator
{
    public class TableOfContents
    {
        public IList<TableOfContentsEntry> Chapters { get; internal set; }
    }
}