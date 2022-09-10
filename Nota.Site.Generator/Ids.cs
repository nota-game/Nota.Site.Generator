using Stasistium.Documents;

using System;
using System.Collections.Generic;
using System.Linq;

namespace Nota.Site.Generator
{



    public class ContentVersions
    {
        private readonly IEnumerable<BookVersion> enumerable;

        public ContentVersions(IEnumerable<BookVersion> enumerable)
        {
            this.enumerable = enumerable.ToArray();
        }

        public IEnumerable<BookVersion> Versions => enumerable;
    }




    public class Config
    {
        public Stasistium.Stages.GitRepo? ContentRepo { get; set; }
        public Stasistium.Stages.GitRepo? SchemaRepo { get; set; }
        public string? Layouts { get; set; }
        public string? StaticContent { get; set; }
        public string? Host { get; set; }

        public Stasistium.Stages.GitRepo? WebsiteRepo { get; set; }
    }

    internal class GitRefMetadata
    {

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        private GitRefMetadata()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {

        }

        public GitRefMetadata(GitRefStage value)
        {
            Name = value.FrindlyName;
            Type = value.Type;
        }

        public GitRefMetadata(string name, GitRefType type)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Type = type;
        }

        public string Name { get; private set; }
        public GitRefType Type { get; private set; }

        public BookVersion CalculatedVersion
        {
            get
            {
                BookVersion version;
                if (Type == GitRefType.Branch && Name == "master") {
                    version = BookVersion.VNext;
                } else if (Type == GitRefType.Branch) {
                    version = new BookVersion(true, Name);
                } else {
                    version = new BookVersion(false, Name);
                }

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

    public class BookMetadata : IEquatable<BookMetadata?>
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public BookMetadata()
        {

        }
        public BookMetadata(string? location, string? beginning, BookVersion version)
        {
            Location = location;
            Beginning = beginning;
            Version = version;
        }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

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
        public string Abbr => $"{BookType.ToId()}{Number} ({Version})";
        /// <summary>
        /// The Abstract of this book formated as markdown
        /// </summary>
        public string Abstract { get; set; }

        public override string ToString()
        {
            return Title;
        }

        // Generated
        public string? Location { get; }
        public string? Beginning { get; }
        public BookVersion Version { get; }


        public BookMetadata WithLocation(string location) => new BookMetadata(location, Beginning, Version)
        {
            Title = Title,
            Number = Number,
            Cover = Cover,
            BookType = BookType,
            Abstract = Abstract,
        };
        public BookMetadata WithBeginning(string beginning) => new BookMetadata(Location, beginning, Version)
        {
            Title = Title,
            Number = Number,
            Cover = Cover,
            BookType = BookType,
            Abstract = Abstract,
        };
        public BookMetadata WithVersion(BookVersion version) => new BookMetadata(Location, Beginning, version)
        {
            Title = Title,
            Number = Number,
            Cover = Cover,
            BookType = BookType,
            Abstract = Abstract,
        };

        public override bool Equals(object? obj)
        {
            return Equals(obj as BookMetadata);
        }

        public bool Equals(BookMetadata? other)
        {
            return other != null &&
                   Title == other.Title &&
                   Number == other.Number &&
                   BookType == other.BookType &&
                   Version.Equals(other.Version);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Title, Number, BookType, Version);
        }

        public static bool operator ==(BookMetadata? left, BookMetadata? right)
        {
            return EqualityComparer<BookMetadata>.Default.Equals(left, right);
        }

        public static bool operator !=(BookMetadata? left, BookMetadata? right)
        {
            return !(left == right);
        }
    }

    public class LicencedFiles
    {
        public IList<LicenseInfo> LicenseInfos { get; set; } = Array.Empty<LicenseInfo>();
    }

    public class LicenseInfo
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public string Id { get; set; }
        public MetadataContainer Metadata { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    }

    public class AllBooksMetadata
    {
        public IList<BookMetadata> Books { get; set; } = Array.Empty<BookMetadata>();
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
        public IList<BookMetadata> Books { get; set; } = Array.Empty<BookMetadata>();
    }


}
