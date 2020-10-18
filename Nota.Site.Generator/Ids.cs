using Stasistium.Documents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Nota.Site.Generator
{



    public class ContentVersions
    {
        private readonly IEnumerable<BookVersion> enumerable;

        public ContentVersions(IEnumerable<BookVersion> enumerable)
        {
            this.enumerable = enumerable.ToArray();
        }

        public IEnumerable<BookVersion> Versions => this.enumerable;
    }


    public class Config
    {
        public string? ContentRepo { get; set; }
        public string? SchemaRepo { get; set; }
        public string? Layouts { get; set; }
        public string? StaticContent { get; set; }
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

        public BookVersion CalculatedVersion
        {
            get
            {
                BookVersion version;
                if (this.Type == GitRefType.Branch && this.Name == "master")
                    version = BookVersion.VNext;
                else if (this.Type == GitRefType.Branch)
                    version = new BookVersion(true, this.Name);
                else
                    version = new BookVersion(false, this.Name);
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
        public BookMetadata()
        {

        }
        public BookMetadata(string? location = null, string? beginning = null, BookVersion version = default)
        {
            this.Location = location;
            this.Beginning = beginning;
            this.Version = version;
        }

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
        public string? Abbr => $"{this.BookType.ToId()}{this.Number} ({this.Version})";
        /// <summary>
        /// The Abstract of this book formated as markdown
        /// </summary>
        public string Abstract { get; set; }

        public override string ToString()
        {
            return this.Title;
        }

        // Generated
        public string? Location { get; }
        public string? Beginning { get; }
        public BookVersion Version { get; }


        public BookMetadata WithLocation(string location) => new BookMetadata(location, this.Beginning, this.Version)
        {
            Title = this.Title,
            Number = this.Number,
            Cover = this.Cover,
            BookType = this.BookType,
            Abstract = this.Abstract,
        };
        public BookMetadata WithBeginning(string beginning) => new BookMetadata(this.Location, beginning, this.Version)
        {
            Title = this.Title,
            Number = this.Number,
            Cover = this.Cover,
            BookType = this.BookType,
            Abstract = this.Abstract,
        };
        public BookMetadata WithVersion(BookVersion version) => new BookMetadata(this.Location, this.Beginning, version)
        {
            Title = this.Title,
            Number = this.Number,
            Cover = this.Cover,
            BookType = this.BookType,
            Abstract = this.Abstract,
        };

        public override bool Equals(object? obj)
        {
            return this.Equals(obj as BookMetadata);
        }

        public bool Equals(BookMetadata? other)
        {
            return other != null &&
                   this.Title == other.Title &&
                   this.Number == other.Number &&
                   this.BookType == other.BookType &&
                   this.Version.Equals(other.Version);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(this.Title, this.Number, this.BookType, this.Version);
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
        public IList<LicenseInfo> LicenseInfos { get; set; }
    }

    public class LicenseInfo
    {
        public string Id { get; set; }
        public MetadataContainer Metadata { get; set; }
        public string Hash { get; set; }
    }

    public class AllBooksMetadata
    {
        public IList<BookMetadata> Books { get; set; }
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
        public IList<BookMetadata> Books { get; set; }
    }


}
