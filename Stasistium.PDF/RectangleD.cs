using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Stasistium.PDF
{
    public struct RectangleD : IEquatable<RectangleD>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref='System.Drawing.RectangleD'/> class.
        /// </summary>
        public static readonly RectangleD Empty;

        private double x; // Do not rename (binary serialization)
        private double y; // Do not rename (binary serialization)
        private double width; // Do not rename (binary serialization)
        private double height; // Do not rename (binary serialization)

        /// <summary>
        /// Initializes a new instance of the <see cref='System.Drawing.RectangleD'/> class with the specified location
        /// and size.
        /// </summary>
        public RectangleD(double x, double y, double width, double height)
        {
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref='System.Drawing.RectangleD'/> class with the specified location
        /// and size.
        /// </summary>
        public RectangleD(PointD location, SizeD size)
        {
            x = location.X;
            y = location.Y;
            width = size.Width;
            height = size.Height;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref='System.Drawing.RectangleD'/> struct from the specified
        /// <see cref="System.Numerics.Vector4"/>.
        /// </summary>
        public RectangleD(Vector4 vector)
        {
            x = vector.X;
            y = vector.Y;
            width = vector.Z;
            height = vector.W;
        }

        /// <summary>
        /// Creates a new <see cref="System.Numerics.Vector4"/> from this <see cref="System.Drawing.RectangleD"/>.
        /// </summary>
        public Vector4 ToVector4() => new Vector4((float)x, (float)y, (float)width, (float)height);

        /// <summary>
        /// Converts the specified <see cref="System.Drawing.RectangleD"/> to a <see cref="System.Numerics.Vector4"/>.
        /// </summary>
        public static explicit operator Vector4(RectangleD rectangle) => rectangle.ToVector4();

        /// <summary>
        /// Converts the specified <see cref="System.Numerics.Vector2"/> to a <see cref="System.Drawing.RectangleD"/>.
        /// </summary>
        public static explicit operator RectangleD(Vector4 vector) => new RectangleD(vector);

        /// <summary>
        /// Creates a new <see cref='System.Drawing.RectangleD'/> with the specified location and size.
        /// </summary>
        public static RectangleD FromLTRB(double left, double top, double right, double bottom) =>
            new RectangleD(left, top, right - left, bottom - top);

        /// <summary>
        /// Gets or sets the coordinates of the upper-left corner of the rectangular region represented by this
        /// <see cref='System.Drawing.RectangleD'/>.
        /// </summary>
        [Browsable(false)]
        public PointD Location
        {
            readonly get => new PointD(X, Y);
            set
            {
                X = value.X;
                Y = value.Y;
            }
        }

        /// <summary>
        /// Gets or sets the size of this <see cref='System.Drawing.RectangleD'/>.
        /// </summary>
        [Browsable(false)]
        public SizeD Size
        {
            readonly get => new SizeD(Width, Height);
            set
            {
                Width = value.Width;
                Height = value.Height;
            }
        }

        /// <summary>
        /// Gets or sets the x-coordinate of the upper-left corner of the rectangular region defined by this
        /// <see cref='System.Drawing.RectangleD'/>.
        /// </summary>
        public double X
        {
            readonly get => x;
            set => x = value;
        }

        /// <summary>
        /// Gets or sets the y-coordinate of the upper-left corner of the rectangular region defined by this
        /// <see cref='System.Drawing.RectangleD'/>.
        /// </summary>
        public double Y
        {
            readonly get => y;
            set => y = value;
        }

        /// <summary>
        /// Gets or sets the width of the rectangular region defined by this <see cref='System.Drawing.RectangleD'/>.
        /// </summary>
        public double Width
        {
            readonly get => width;
            set => width = value;
        }

        /// <summary>
        /// Gets or sets the height of the rectangular region defined by this <see cref='System.Drawing.RectangleD'/>.
        /// </summary>
        public double Height
        {
            readonly get => height;
            set => height = value;
        }

        /// <summary>
        /// Gets the x-coordinate of the upper-left corner of the rectangular region defined by this
        /// <see cref='System.Drawing.RectangleD'/> .
        /// </summary>
        [Browsable(false)]
        public readonly double Left => X;

        /// <summary>
        /// Gets the y-coordinate of the upper-left corner of the rectangular region defined by this
        /// <see cref='System.Drawing.RectangleD'/>.
        /// </summary>
        [Browsable(false)]
        public readonly double Top => Y;

        /// <summary>
        /// Gets the x-coordinate of the lower-right corner of the rectangular region defined by this
        /// <see cref='System.Drawing.RectangleD'/>.
        /// </summary>
        [Browsable(false)]
        public readonly double Right => X + Width;

        /// <summary>
        /// Gets the y-coordinate of the lower-right corner of the rectangular region defined by this
        /// <see cref='System.Drawing.RectangleD'/>.
        /// </summary>
        [Browsable(false)]
        public readonly double Bottom => Y + Height;

        /// <summary>
        /// Tests whether this <see cref='System.Drawing.RectangleD'/> has a <see cref='System.Drawing.RectangleD.Width'/> or a <see cref='System.Drawing.RectangleD.Height'/> of 0.
        /// </summary>
        [Browsable(false)]
        public readonly bool IsEmpty => (Width <= 0) || (Height <= 0);

        /// <summary>
        /// Tests whether <paramref name="obj"/> is a <see cref='System.Drawing.RectangleD'/> with the same location and
        /// size of this <see cref='System.Drawing.RectangleD'/>.
        /// </summary>
        public override readonly bool Equals([NotNullWhen(true)] object? obj) => obj is RectangleD && Equals((RectangleD)obj);

        public readonly bool Equals(RectangleD other) => this == other;

        /// <summary>
        /// Tests whether two <see cref='System.Drawing.RectangleD'/> objects have equal location and size.
        /// </summary>
        public static bool operator ==(RectangleD left, RectangleD right) =>
            left.X == right.X && left.Y == right.Y && left.Width == right.Width && left.Height == right.Height;

        /// <summary>
        /// Tests whether two <see cref='System.Drawing.RectangleD'/> objects differ in location or size.
        /// </summary>
        public static bool operator !=(RectangleD left, RectangleD right) => !(left == right);

        /// <summary>
        /// Determines if the specified point is contained within the rectangular region defined by this
        /// <see cref='System.Drawing.Rectangle'/> .
        /// </summary>
        public readonly bool Contains(double x, double y) => X <= x && x < X + Width && Y <= y && y < Y + Height;

        /// <summary>
        /// Determines if the specified point is contained within the rectangular region defined by this
        /// <see cref='System.Drawing.Rectangle'/> .
        /// </summary>
        public readonly bool Contains(PointD pt) => Contains(pt.X, pt.Y);

        /// <summary>
        /// Determines if the rectangular region represented by <paramref name="rect"/> is entirely contained within
        /// the rectangular region represented by this <see cref='System.Drawing.Rectangle'/> .
        /// </summary>
        public readonly bool Contains(RectangleD rect) =>
            (X <= rect.X) && (rect.X + rect.Width <= X + Width) && (Y <= rect.Y) && (rect.Y + rect.Height <= Y + Height);

        /// <summary>
        /// Gets the hash code for this <see cref='System.Drawing.RectangleD'/>.
        /// </summary>
        public override readonly int GetHashCode() => HashCode.Combine(X, Y, Width, Height);

        /// <summary>
        /// Inflates this <see cref='System.Drawing.Rectangle'/> by the specified amount.
        /// </summary>
        public void Inflate(double x, double y)
        {
            X -= x;
            Y -= y;
            Width += 2 * x;
            Height += 2 * y;
        }

        /// <summary>
        /// Inflates this <see cref='System.Drawing.Rectangle'/> by the specified amount.
        /// </summary>
        public void Inflate(SizeD size) => Inflate(size.Width, size.Height);

        /// <summary>
        /// Creates a <see cref='System.Drawing.Rectangle'/> that is inflated by the specified amount.
        /// </summary>
        public static RectangleD Inflate(RectangleD rect, double x, double y)
        {
            RectangleD r = rect;
            r.Inflate(x, y);
            return r;
        }

        /// <summary>
        /// Creates a Rectangle that represents the intersection between this Rectangle and rect.
        /// </summary>
        public void Intersect(RectangleD rect)
        {
            RectangleD result = Intersect(rect, this);

            X = result.X;
            Y = result.Y;
            Width = result.Width;
            Height = result.Height;
        }

        /// <summary>
        /// Creates a rectangle that represents the intersection between a and b. If there is no intersection, an
        /// empty rectangle is returned.
        /// </summary>
        public static RectangleD Intersect(RectangleD a, RectangleD b)
        {
            double x1 = Math.Max(a.X, b.X);
            double x2 = Math.Min(a.X + a.Width, b.X + b.Width);
            double y1 = Math.Max(a.Y, b.Y);
            double y2 = Math.Min(a.Y + a.Height, b.Y + b.Height);

            if (x2 >= x1 && y2 >= y1)
            {
                return new RectangleD(x1, y1, x2 - x1, y2 - y1);
            }

            return Empty;
        }

        /// <summary>
        /// Determines if this rectangle intersects with rect.
        /// </summary>
        public readonly bool IntersectsWith(RectangleD rect) =>
            (rect.X < X + Width) && (X < rect.X + rect.Width) && (rect.Y < Y + Height) && (Y < rect.Y + rect.Height);

        /// <summary>
        /// Creates a rectangle that represents the union between a and b.
        /// </summary>
        public static RectangleD Union(RectangleD a, RectangleD b)
        {
            double x1 = Math.Min(a.X, b.X);
            double x2 = Math.Max(a.X + a.Width, b.X + b.Width);
            double y1 = Math.Min(a.Y, b.Y);
            double y2 = Math.Max(a.Y + a.Height, b.Y + b.Height);

            return new RectangleD(x1, y1, x2 - x1, y2 - y1);
        }

        /// <summary>
        /// Adjusts the location of this rectangle by the specified amount.
        /// </summary>
        public void Offset(PointD pos) => Offset(pos.X, pos.Y);

        /// <summary>
        /// Adjusts the location of this rectangle by the specified amount.
        /// </summary>
        public void Offset(double x, double y)
        {
            X += x;
            Y += y;
        }

        /// <summary>
        /// Converts the specified <see cref='System.Drawing.Rectangle'/> to a
        /// <see cref='System.Drawing.RectangleD'/>.
        /// </summary>
        public static implicit operator RectangleD(Rectangle r) => new RectangleD(r.X, r.Y, r.Width, r.Height);

        /// <summary>
        /// Converts the specified <see cref='System.Drawing.Rectangle'/> to a
        /// <see cref='System.Drawing.RectangleD'/>.
        /// </summary>
        public static implicit operator RectangleD(RectangleF r) => new RectangleD(r.X, r.Y, r.Width, r.Height);

        /// <summary>
        /// Converts the <see cref='System.Drawing.RectangleD.Location'/> and <see cref='System.Drawing.RectangleD.Size'/>
        /// of this <see cref='System.Drawing.RectangleD'/> to a human-readable string.
        /// </summary>
        public override readonly string ToString() => $"{{X={X},Y={Y},Width={Width},Height={Height}}}";
    }

    public struct PointD : IEquatable<PointD>
    {
        /// <summary>
        /// Creates a new instance of the <see cref='System.Drawing.PointD'/> class with member data left uninitialized.
        /// </summary>
        public static readonly PointD Empty;
        private double x; // Do not rename (binary serialization)
        private double y; // Do not rename (binary serialization)

        /// <summary>
        /// Initializes a new instance of the <see cref='System.Drawing.PointD'/> class with the specified coordinates.
        /// </summary>
        public PointD(double x, double y)
        {
            this.x = x;
            this.y = y;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref='System.Drawing.PointD'/> struct from the specified
        /// <see cref="System.Numerics.Vector2"/>.
        /// </summary>
        public PointD(Vector2 vector)
        {
            x = vector.X;
            y = vector.Y;
        }

        /// <summary>
        /// Creates a new <see cref="System.Numerics.Vector2"/> from this <see cref="System.Drawing.PointD"/>.
        /// </summary>
        public Vector2 ToVector2() => new Vector2((float)x, (float)y);

        /// <summary>
        /// Gets a value indicating whether this <see cref='System.Drawing.PointD'/> is empty.
        /// </summary>
        [Browsable(false)]
        public readonly bool IsEmpty => x == 0f && y == 0f;

        /// <summary>
        /// Gets the x-coordinate of this <see cref='System.Drawing.PointD'/>.
        /// </summary>
        public double X
        {
            readonly get => x;
            set => x = value;
        }

        /// <summary>
        /// Gets the y-coordinate of this <see cref='System.Drawing.PointD'/>.
        /// </summary>
        public double Y
        {
            readonly get => y;
            set => y = value;
        }

        /// <summary>
        /// Converts the specified <see cref="System.Drawing.PointD"/> to a <see cref="System.Numerics.Vector2"/>.
        /// </summary>
        public static explicit operator Vector2(PointD point) => point.ToVector2();

        /// <summary>
        /// Converts the specified <see cref="System.Numerics.Vector2"/> to a <see cref="System.Drawing.PointD"/>.
        /// </summary>
        public static explicit operator PointD(Vector2 vector) => new PointD(vector);

        /// <summary>
        /// Translates a <see cref='System.Drawing.PointD'/> by a given <see cref='System.Drawing.Size'/> .
        /// </summary>
        public static PointD operator +(PointD pt, Size sz) => Add(pt, sz);

        /// <summary>
        /// Translates a <see cref='System.Drawing.PointD'/> by the negative of a given <see cref='System.Drawing.Size'/> .
        /// </summary>
        public static PointD operator -(PointD pt, Size sz) => Subtract(pt, sz);

        /// <summary>
        /// Translates a <see cref='System.Drawing.PointD'/> by a given <see cref='System.Drawing.SizeD'/> .
        /// </summary>
        public static PointD operator +(PointD pt, SizeD sz) => Add(pt, sz);

        /// <summary>
        /// Translates a <see cref='System.Drawing.PointD'/> by the negative of a given <see cref='System.Drawing.SizeD'/> .
        /// </summary>
        public static PointD operator -(PointD pt, SizeD sz) => Subtract(pt, sz);

        /// <summary>
        /// Compares two <see cref='System.Drawing.PointD'/> objects. The result specifies whether the values of the
        /// <see cref='System.Drawing.PointD.X'/> and <see cref='System.Drawing.PointD.Y'/> properties of the two
        /// <see cref='System.Drawing.PointD'/> objects are equal.
        /// </summary>
        public static bool operator ==(PointD left, PointD right) => left.X == right.X && left.Y == right.Y;

        /// <summary>
        /// Compares two <see cref='System.Drawing.PointD'/> objects. The result specifies whether the values of the
        /// <see cref='System.Drawing.PointD.X'/> or <see cref='System.Drawing.PointD.Y'/> properties of the two
        /// <see cref='System.Drawing.PointD'/> objects are unequal.
        /// </summary>
        public static bool operator !=(PointD left, PointD right) => !(left == right);

        /// <summary>
        /// Translates a <see cref='System.Drawing.PointD'/> by a given <see cref='System.Drawing.Size'/> .
        /// </summary>
        public static PointD Add(PointD pt, Size sz) => new PointD(pt.X + sz.Width, pt.Y + sz.Height);

        /// <summary>
        /// Translates a <see cref='System.Drawing.PointD'/> by the negative of a given <see cref='System.Drawing.Size'/> .
        /// </summary>
        public static PointD Subtract(PointD pt, Size sz) => new PointD(pt.X - sz.Width, pt.Y - sz.Height);

        /// <summary>
        /// Translates a <see cref='System.Drawing.PointD'/> by a given <see cref='System.Drawing.SizeD'/> .
        /// </summary>
        public static PointD Add(PointD pt, SizeD sz) => new PointD(pt.X + sz.Width, pt.Y + sz.Height);

        /// <summary>
        /// Translates a <see cref='System.Drawing.PointD'/> by the negative of a given <see cref='System.Drawing.SizeD'/> .
        /// </summary>
        public static PointD Subtract(PointD pt, SizeD sz) => new PointD(pt.X - sz.Width, pt.Y - sz.Height);

        public override readonly bool Equals([NotNullWhen(true)] object? obj) => obj is PointD && Equals((PointD)obj);

        public readonly bool Equals(PointD other) => this == other;

        public override readonly int GetHashCode() => HashCode.Combine(X.GetHashCode(), Y.GetHashCode());

        public override readonly string ToString() => $"{{X={x}, Y={y}}}";
    }

    public struct SizeD : IEquatable<SizeD>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref='System.Drawing.SizeD'/> class.
        /// </summary>
        public static readonly SizeD Empty;
        private double width; // Do not rename (binary serialization)
        private double height; // Do not rename (binary serialization)

        /// <summary>
        /// Initializes a new instance of the <see cref='System.Drawing.SizeD'/> class from the specified
        /// existing <see cref='System.Drawing.SizeD'/>.
        /// </summary>
        public SizeD(SizeD size)
        {
            width = size.width;
            height = size.height;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref='System.Drawing.SizeD'/> class from the specified
        /// <see cref='System.Drawing.PointD'/>.
        /// </summary>
        public SizeD(PointD pt)
        {
            width = pt.X;
            height = pt.Y;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref='System.Drawing.SizeD'/> struct from the specified
        /// <see cref="System.Numerics.Vector2"/>.
        /// </summary>
        public SizeD(Vector2 vector)
        {
            width = vector.X;
            height = vector.Y;
        }

        /// <summary>
        /// Creates a new <see cref="System.Numerics.Vector2"/> from this <see cref="System.Drawing.SizeD"/>.
        /// </summary>
        public Vector2 ToVector2() => new Vector2((float)width, (float)height);

        /// <summary>
        /// Initializes a new instance of the <see cref='System.Drawing.SizeD'/> class from the specified dimensions.
        /// </summary>
        public SizeD(double width, double height)
        {
            this.width = width;
            this.height = height;
        }

        /// <summary>
        /// Converts the specified <see cref="System.Drawing.SizeD"/> to a <see cref="System.Numerics.Vector2"/>.
        /// </summary>
        public static explicit operator Vector2(SizeD size) => size.ToVector2();

        /// <summary>
        /// Converts the specified <see cref="System.Numerics.Vector2"/> to a <see cref="System.Drawing.SizeD"/>.
        /// </summary>
        public static explicit operator SizeD(Vector2 vector) => new SizeD(vector);

        /// <summary>
        /// Performs vector addition of two <see cref='System.Drawing.SizeD'/> objects.
        /// </summary>
        public static SizeD operator +(SizeD sz1, SizeD sz2) => Add(sz1, sz2);

        /// <summary>
        /// Contracts a <see cref='System.Drawing.SizeD'/> by another <see cref='System.Drawing.SizeD'/>
        /// </summary>
        public static SizeD operator -(SizeD sz1, SizeD sz2) => Subtract(sz1, sz2);

        /// <summary>
        /// Multiplies <see cref="SizeD"/> by a <see cref="double"/> producing <see cref="SizeD"/>.
        /// </summary>
        /// <param name="left">Multiplier of type <see cref="double"/>.</param>
        /// <param name="right">Multiplicand of type <see cref="SizeD"/>.</param>
        /// <returns>Product of type <see cref="SizeD"/>.</returns>
        public static SizeD operator *(double left, SizeD right) => Multiply(right, left);

        /// <summary>
        /// Multiplies <see cref="SizeD"/> by a <see cref="double"/> producing <see cref="SizeD"/>.
        /// </summary>
        /// <param name="left">Multiplicand of type <see cref="SizeD"/>.</param>
        /// <param name="right">Multiplier of type <see cref="double"/>.</param>
        /// <returns>Product of type <see cref="SizeD"/>.</returns>
        public static SizeD operator *(SizeD left, double right) => Multiply(left, right);

        /// <summary>
        /// Divides <see cref="SizeD"/> by a <see cref="double"/> producing <see cref="SizeD"/>.
        /// </summary>
        /// <param name="left">Dividend of type <see cref="SizeD"/>.</param>
        /// <param name="right">Divisor of type <see cref="int"/>.</param>
        /// <returns>Result of type <see cref="SizeD"/>.</returns>
        public static SizeD operator /(SizeD left, double right)
            => new SizeD(left.width / right, left.height / right);

        /// <summary>
        /// Tests whether two <see cref='System.Drawing.SizeD'/> objects are identical.
        /// </summary>
        public static bool operator ==(SizeD sz1, SizeD sz2) => sz1.Width == sz2.Width && sz1.Height == sz2.Height;

        /// <summary>
        /// Tests whether two <see cref='System.Drawing.SizeD'/> objects are different.
        /// </summary>
        public static bool operator !=(SizeD sz1, SizeD sz2) => !(sz1 == sz2);

        /// <summary>
        /// Converts the specified <see cref='System.Drawing.SizeD'/> to a <see cref='System.Drawing.PointD'/>.
        /// </summary>
        public static explicit operator PointD(SizeD size) => new PointD(size.Width, size.Height);

        /// <summary>
        /// Tests whether this <see cref='System.Drawing.SizeD'/> has zero width and height.
        /// </summary>
        [Browsable(false)]
        public readonly bool IsEmpty => width == 0 && height == 0;

        /// <summary>
        /// Represents the horizontal component of this <see cref='System.Drawing.SizeD'/>.
        /// </summary>
        public double Width
        {
            readonly get => width;
            set => width = value;
        }

        /// <summary>
        /// Represents the vertical component of this <see cref='System.Drawing.SizeD'/>.
        /// </summary>
        public double Height
        {
            readonly get => height;
            set => height = value;
        }

        /// <summary>
        /// Performs vector addition of two <see cref='System.Drawing.SizeD'/> objects.
        /// </summary>
        public static SizeD Add(SizeD sz1, SizeD sz2) => new SizeD(sz1.Width + sz2.Width, sz1.Height + sz2.Height);

        /// <summary>
        /// Contracts a <see cref='System.Drawing.SizeD'/> by another <see cref='System.Drawing.SizeD'/>.
        /// </summary>
        public static SizeD Subtract(SizeD sz1, SizeD sz2) => new SizeD(sz1.Width - sz2.Width, sz1.Height - sz2.Height);

        /// <summary>
        /// Tests to see whether the specified object is a <see cref='System.Drawing.SizeD'/>  with the same dimensions
        /// as this <see cref='System.Drawing.SizeD'/>.
        /// </summary>
        public override readonly bool Equals([NotNullWhen(true)] object? obj) => obj is SizeD && Equals((SizeD)obj);

        public readonly bool Equals(SizeD other) => this == other;

        public override readonly int GetHashCode() => HashCode.Combine(Width, Height);

        public readonly PointD ToPointD() => (PointD)this;

        public readonly Size ToSize() => new((int)this.height, (int)this.Width);

        /// <summary>
        /// Creates a human-readable string that represents this <see cref='System.Drawing.SizeD'/>.
        /// </summary>
        public override readonly string ToString() => $"{{Width={width}, Height={height}}}";

        /// <summary>
        /// Multiplies <see cref="SizeD"/> by a <see cref="double"/> producing <see cref="SizeD"/>.
        /// </summary>
        /// <param name="size">Multiplicand of type <see cref="SizeD"/>.</param>
        /// <param name="multiplier">Multiplier of type <see cref="double"/>.</param>
        /// <returns>Product of type SizeD.</returns>
        private static SizeD Multiply(SizeD size, double multiplier) =>
            new SizeD(size.width * multiplier, size.height * multiplier);
    }

    public static class SpanSplitExtensions
    {
        public static SpanWordEnumerator Split(this ReadOnlySpan<char> txt) => new(txt);
    }

}
