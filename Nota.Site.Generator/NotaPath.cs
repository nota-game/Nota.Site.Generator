using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Nota.Site.Generator
{
    public static class NotaPath
    {
        private const char DirectorySeparatorChar = '/';

        public static string Combine(string path1, string path2) => Combine(path1.AsSpan(), path2.AsSpan(), ReadOnlySpan<char>.Empty, ReadOnlySpan<char>.Empty);
        public static string Combine(string path1, string path2, string path3) => Combine(path1.AsSpan(), path2.AsSpan(), path3.AsSpan(), ReadOnlySpan<char>.Empty);
        public static string Combine(string path1, string path2, string path3, string path4) => Combine(path1.AsSpan(), path2.AsSpan(), path3.AsSpan(), path4.AsSpan());


        public static string Combine(ReadOnlySpan<char> path1, ReadOnlySpan<char> path2) => Combine(path1, path2, ReadOnlySpan<char>.Empty, ReadOnlySpan<char>.Empty);
        public static string Combine(ReadOnlySpan<char> path1, ReadOnlySpan<char> path2, ReadOnlySpan<char> path3) => Combine(path1, path2, path3, ReadOnlySpan<char>.Empty);
        public static string Combine(ReadOnlySpan<char> path1, ReadOnlySpan<char> path2, ReadOnlySpan<char> path3, ReadOnlySpan<char> path4)
        {

            if (IsPathRooted(path4))
                return path4.ToString();

            if (IsPathRooted(path3))
                return Combine(path3, path4, ReadOnlySpan<char>.Empty, ReadOnlySpan<char>.Empty);

            if (IsPathRooted(path2))
                return Combine(path2, path3, path4, ReadOnlySpan<char>.Empty);

            var numberOfAditionalSeperators = 0;

            var appand1 = !IsEndingWithSeperator(path1) && (path2.Length > 0 || path3.Length > 0 || path4.Length > 0);
            var appand2 = !IsEndingWithSeperator(path2) && (path3.Length > 0 || path4.Length > 0);
            var appand3 = !IsEndingWithSeperator(path3) && (path4.Length > 0);

            if (appand1)
                numberOfAditionalSeperators++;
            if (appand2)
                numberOfAditionalSeperators++;
            if (appand3)
                numberOfAditionalSeperators++;

            var array = new char[path1.Length + path2.Length + path3.Length + path4.Length + numberOfAditionalSeperators].AsSpan();

            var pos = 0;

            path1.CopyTo(array.Slice(pos));
            pos += path1.Length;
            if (appand1)
            {
                array[pos] = '/';
                pos++;
            }

            path2.CopyTo(array.Slice(pos));
            pos += path2.Length;
            if (appand2)
            {
                array[pos] = '/';
                pos++;
            }

            path3.CopyTo(array.Slice(pos));
            pos += path3.Length;
            if (appand3)
            {
                array[pos] = '/';
                pos++;
            }

            path4.CopyTo(array.Slice(pos));
            pos += path4.Length;

            Debug.Assert(array.Length == pos);




            return new string(array);
        }

        public static string GetIdWithoutExtension(string id)
        {
            var lastIndex = id.LastIndexOf('/');
            lastIndex = id.IndexOf('.', lastIndex + 1);
            return id.Substring(0, lastIndex);
        }
        public static string GetExtension(string id)
        {
            var lastIndex = id.LastIndexOf('/');
            lastIndex = id.IndexOf('.', lastIndex + 1);
            return id.Substring(lastIndex);
        }

        public static bool Is(string id, string match)
        {
            if (id == match)
                return true;
            var name = GetIdWithoutExtension(id);
            var extension = GetExtension(id);
            var index = extension.IndexOf('.', 1);
            if (index > -1)
            {
                var v = (name + extension.Substring(index));
                return v == match;

            }
            else
                return name == match;
        }

        #region .NetCoreCode
        // code from source.dot.net changed to use '/' intead of '\'
        // Licensed to the .NET Foundation under one or more agreements.
        // The .NET Foundation licenses this file to you under the MIT license.
        // See the LICENSE file in the project root of System.Private.CoreLib.
        public static string Combine(params string[] paths)
        {
            if (paths == null)
            {
                throw new ArgumentNullException(nameof(paths));
            }

            int maxSize = 0;
            int firstComponent = 0;

            // We have two passes, the first calculates how large a buffer to allocate and does some precondition
            // checks on the paths passed in.  The second actually does the combination.

            for (int i = 0; i < paths.Length; i++)
            {
                if (paths[i] == null)
                {
                    throw new ArgumentNullException(nameof(paths));
                }

                if (paths[i].Length == 0)
                {
                    continue;
                }

                if (IsPathRooted(paths[i]))
                {
                    firstComponent = i;
                    maxSize = paths[i].Length;
                }
                else
                {
                    maxSize += paths[i].Length;
                }

                if (!IsEndingWithSeperator(paths[i].AsSpan()))
                    maxSize++;
            }

            var builder = new ValueStringBuilder(stackalloc char[260]); // MaxShortPath on Windows
            builder.EnsureCapacity(maxSize);

            for (int i = firstComponent; i < paths.Length; i++)
            {
                if (paths[i].Length == 0)
                {
                    continue;
                }

                if (builder.Length == 0)
                {
                    builder.Append(paths[i]);
                }
                else
                {
                    char ch = builder[builder.Length - 1];
                    if (!IsEndingWithSeperator(builder.AsSpan()))
                    {
                        builder.Append(DirectorySeparatorChar);
                    }

                    builder.Append(paths[i]);
                }
            }

            return builder.ToString();
        }


        public static bool IsPathRooted([NotNullWhen(true)] string? path)
        {
            if (path == null)
                return false;

            return IsPathRooted(path.AsSpan());
        }

        public static bool IsPathRooted(in ReadOnlySpan<char> path)
        {
            return path.Length > 0 && path[0] == DirectorySeparatorChar;
        }
        public static bool IsEndingWithSeperator(in ReadOnlySpan<char> path)
        {
            return path.Length > 0 && path[path.Length - 1] == DirectorySeparatorChar;
        }


        internal ref partial struct ValueStringBuilder
        {
            private char[]? _arrayToReturnToPool;
            private Span<char> _chars;
            private int _pos;

            public ValueStringBuilder(Span<char> initialBuffer)
            {
                this._arrayToReturnToPool = null;
                this._chars = initialBuffer;
                this._pos = 0;
            }

            public ValueStringBuilder(int initialCapacity)
            {
                this._arrayToReturnToPool = ArrayPool<char>.Shared.Rent(initialCapacity);
                this._chars = this._arrayToReturnToPool;
                this._pos = 0;
            }

            public int Length
            {
                get => this._pos;
                set
                {
                    Debug.Assert(value >= 0);
                    Debug.Assert(value <= this._chars.Length);
                    this._pos = value;
                }
            }

            public int Capacity => this._chars.Length;

            public void EnsureCapacity(int capacity)
            {
                if (capacity > this._chars.Length)
                    this.Grow(capacity - this._pos);
            }

            public ref char this[int index]
            {
                get
                {
                    Debug.Assert(index < this._pos);
                    return ref this._chars[index];
                }
            }

            public override string ToString()
            {
                string s = this._chars.Slice(0, this._pos).ToString();
                this.Dispose();
                return s;
            }

            /// <summary>Returns the underlying storage of the builder.</summary>
            public Span<char> RawChars => this._chars;

            /// <summary>
            /// Returns a span around the contents of the builder.
            /// </summary>
            /// <param name="terminate">Ensures that the builder has a null char after <see cref="Length"/></param>
            public ReadOnlySpan<char> AsSpan(bool terminate)
            {
                if (terminate)
                {
                    this.EnsureCapacity(this.Length + 1);
                    this._chars[this.Length] = '\0';
                }
                return this._chars.Slice(0, this._pos);
            }

            public ReadOnlySpan<char> AsSpan() => this._chars.Slice(0, this._pos);
            public ReadOnlySpan<char> AsSpan(int start) => this._chars.Slice(start, this._pos - start);
            public ReadOnlySpan<char> AsSpan(int start, int length) => this._chars.Slice(start, length);

            public bool TryCopyTo(Span<char> destination, out int charsWritten)
            {
                if (this._chars.Slice(0, this._pos).TryCopyTo(destination))
                {
                    charsWritten = this._pos;
                    this.Dispose();
                    return true;
                }
                else
                {
                    charsWritten = 0;
                    this.Dispose();
                    return false;
                }
            }

            public void Insert(int index, char value, int count)
            {
                if (this._pos > this._chars.Length - count)
                {
                    this.Grow(count);
                }

                int remaining = this._pos - index;
                this._chars.Slice(index, remaining).CopyTo(this._chars.Slice(index + count));
                this._chars.Slice(index, count).Fill(value);
                this._pos += count;
            }

            public void Insert(int index, string? s)
            {
                if (s == null)
                {
                    return;
                }

                int count = s.Length;

                if (this._pos > (this._chars.Length - count))
                {
                    this.Grow(count);
                }

                int remaining = this._pos - index;
                this._chars.Slice(index, remaining).CopyTo(this._chars.Slice(index + count));
                s.AsSpan().CopyTo(this._chars.Slice(index));
                this._pos += count;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Append(char c)
            {
                int pos = this._pos;
                if ((uint)pos < (uint)this._chars.Length)
                {
                    this._chars[pos] = c;
                    this._pos = pos + 1;
                }
                else
                {
                    this.GrowAndAppend(c);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Append(string? s)
            {
                if (s == null)
                {
                    return;
                }

                int pos = this._pos;
                if (s.Length == 1 && (uint)pos < (uint)this._chars.Length) // very common case, e.g. appending strings from NumberFormatInfo like separators, percent symbols, etc.
                {
                    this._chars[pos] = s[0];
                    this._pos = pos + 1;
                }
                else
                {
                    this.AppendSlow(s);
                }
            }

            private void AppendSlow(string s)
            {
                int pos = this._pos;
                if (pos > this._chars.Length - s.Length)
                {
                    this.Grow(s.Length);
                }

                s.AsSpan().CopyTo(this._chars.Slice(pos));
                this._pos += s.Length;
            }

            public void Append(char c, int count)
            {
                if (this._pos > this._chars.Length - count)
                {
                    this.Grow(count);
                }

                Span<char> dst = this._chars.Slice(this._pos, count);
                for (int i = 0; i < dst.Length; i++)
                {
                    dst[i] = c;
                }
                this._pos += count;
            }



            public void Append(ReadOnlySpan<char> value)
            {
                int pos = this._pos;
                if (pos > this._chars.Length - value.Length)
                {
                    this.Grow(value.Length);
                }

                value.CopyTo(this._chars.Slice(this._pos));
                this._pos += value.Length;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Span<char> AppendSpan(int length)
            {
                int origPos = this._pos;
                if (origPos > this._chars.Length - length)
                {
                    this.Grow(length);
                }

                this._pos = origPos + length;
                return this._chars.Slice(origPos, length);
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            private void GrowAndAppend(char c)
            {
                this.Grow(1);
                this.Append(c);
            }

            /// <summary>
            /// Resize the internal buffer either by doubling current buffer size or
            /// by adding <paramref name="additionalCapacityBeyondPos"/> to
            /// <see cref="_pos"/> whichever is greater.
            /// </summary>
            /// <param name="additionalCapacityBeyondPos">
            /// Number of chars requested beyond current position.
            /// </param>
            [MethodImpl(MethodImplOptions.NoInlining)]
            private void Grow(int additionalCapacityBeyondPos)
            {
                Debug.Assert(additionalCapacityBeyondPos > 0);
                Debug.Assert(this._pos > this._chars.Length - additionalCapacityBeyondPos, "Grow called incorrectly, no resize is needed.");

                char[] poolArray = ArrayPool<char>.Shared.Rent(Math.Max(this._pos + additionalCapacityBeyondPos, this._chars.Length * 2));

                this._chars.Slice(0, this._pos).CopyTo(poolArray);

                char[]? toReturn = this._arrayToReturnToPool;
                this._chars = this._arrayToReturnToPool = poolArray;
                if (toReturn != null)
                {
                    ArrayPool<char>.Shared.Return(toReturn);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                char[]? toReturn = this._arrayToReturnToPool;
                this = default; // for safety, to avoid using pooled array if this instance is erroneously appended to again
                if (toReturn != null)
                {
                    ArrayPool<char>.Shared.Return(toReturn);
                }
            }
        }

        #endregion
    }
}