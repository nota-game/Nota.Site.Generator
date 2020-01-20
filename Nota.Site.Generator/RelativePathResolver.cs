﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Immutable;
using Stasistium;

namespace Nota.Site.Generator.Markdown.Blocks
{
    internal class RelativePathResolver
    {
        private readonly string relativeTo;
        private readonly Dictionary<string, string> lookup;

        public string this[string index] => this.lookup[index];


        public RelativePathResolver(string relativeTo, IEnumerable<string> documents)
        {
            this.relativeTo = relativeTo;
            this.lookup = documents.SelectMany(this.GetPathes).ToDictionary(x => x.relativeOrFullPath, x => x.fullPath);
        }

        private IEnumerable<(string relativeOrFullPath, string fullPath)> GetPathes(string fullpath)
        {
            yield return ("/" + fullpath, fullpath);

            var currentFolder = System.IO.Path.GetDirectoryName(this.relativeTo)?.Replace('\\', '/');
            if (currentFolder is null)
                yield break;
            if (!currentFolder.EndsWith('/'))
                currentFolder += '/';
            if (fullpath.StartsWith(currentFolder))
                yield return (fullpath.Substring(currentFolder.Length), fullpath);
        }
    }


}
