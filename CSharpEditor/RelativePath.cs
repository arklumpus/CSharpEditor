using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpEditor
{
    // Adapted from https://referencesource.microsoft.com/#mscorlib/system/io/pathinternal.cs, https://source.dot.net/#System.Private.CoreLib/PathInternal.cs, and https://source.dot.net/#System.Private.CoreLib/PathInternal.Unix.cs

    internal static class RelativePath
    {
        /// <summary>
        /// Gets the length of the root of the path (drive, share, etc.).
        /// </summary>
        [System.Security.SecuritySafeCritical]
        internal unsafe static int GetRootLengthWindows(string path)
        {
            fixed (char* value = path)
            {
                return (int)GetRootLength(value, (ulong)path.Length);
            }
        }

        [System.Security.SecurityCritical]
        private unsafe static bool StartsWithOrdinal(char* source, ulong sourceLength, string value)
        {
            if (sourceLength < (ulong)value.Length) return false;
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] != source[i]) return false;
            }
            return true;
        }

        internal const string ExtendedPathPrefix = @"\\?\";
        internal const string UncExtendedPathPrefix = @"\\?\UNC\";

        [System.Security.SecurityCritical]
        private unsafe static uint GetRootLength(char* path, ulong pathLength)
        {
            uint i = 0;
            uint volumeSeparatorLength = 2;  // Length to the colon "C:"
            uint uncRootLength = 2;          // Length to the start of the server name "\\"

            bool extendedSyntax = StartsWithOrdinal(path, pathLength, ExtendedPathPrefix);
            bool extendedUncSyntax = StartsWithOrdinal(path, pathLength, UncExtendedPathPrefix);
            if (extendedSyntax)
            {
                // Shift the position we look for the root from to account for the extended prefix
                if (extendedUncSyntax)
                {
                    // "\\" -> "\\?\UNC\"
                    uncRootLength = (uint)UncExtendedPathPrefix.Length;
                }
                else
                {
                    // "C:" -> "\\?\C:"
                    volumeSeparatorLength += (uint)ExtendedPathPrefix.Length;
                }
            }

            if ((!extendedSyntax || extendedUncSyntax) && pathLength > 0 && IsDirectorySeparator(path[0]))
            {
                // UNC or simple rooted path (e.g. "\foo", NOT "\\?\C:\foo")

                i = 1; //  Drive rooted (\foo) is one character
                if (extendedUncSyntax || (pathLength > 1 && IsDirectorySeparator(path[1])))
                {
                    // UNC (\\?\UNC\ or \\), scan past the next two directory separators at most
                    // (e.g. to \\?\UNC\Server\Share or \\Server\Share\)
                    i = uncRootLength;
                    int n = 2; // Maximum separators to skip
                    while (i < pathLength && (!IsDirectorySeparator(path[i]) || --n > 0)) i++;
                }
            }
            else if (pathLength >= volumeSeparatorLength && path[volumeSeparatorLength - 1] == System.IO.Path.VolumeSeparatorChar)
            {
                // Path is at least longer than where we expect a colon, and has a colon (\\?\A:, A:)
                // If the colon is followed by a directory separator, move past it
                i = volumeSeparatorLength;
                if (pathLength >= volumeSeparatorLength + 1 && IsDirectorySeparator(path[volumeSeparatorLength])) i++;
            }
            return i;
        }

        internal static int GetRootLength(string path)
        {
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                return GetRootLengthWindows(path);
            }
            else
            {
                return path.Length > 0 && IsDirectorySeparator(path[0]) ? 1 : 0;
            }
        }
        internal static bool IsDirectorySeparator(char c)
        {
            return c == System.IO.Path.DirectorySeparatorChar || c == System.IO.Path.AltDirectorySeparatorChar;
        }

        internal static bool AreRootsEqual(string first, string second, StringComparison comparisonType)
        {
            int firstRootLength = GetRootLength(first);
            int secondRootLength = GetRootLength(second);

            return firstRootLength == secondRootLength
                && string.Compare(
                    strA: first,
                    indexA: 0,
                    strB: second,
                    indexB: 0,
                    length: firstRootLength,
                    comparisonType: comparisonType) == 0;
        }

        /// <summary>
        /// Gets the count of common characters from the left optionally ignoring case
        /// </summary>
        internal static unsafe int EqualStartingCharacterCount(string first, string second, bool ignoreCase)
        {
            if (string.IsNullOrEmpty(first) || string.IsNullOrEmpty(second)) return 0;

            int commonChars = 0;

            fixed (char* f = first)
            fixed (char* s = second)
            {
                char* l = f;
                char* r = s;
                char* leftEnd = l + first.Length;
                char* rightEnd = r + second.Length;

                while (l != leftEnd && r != rightEnd
                    && (*l == *r || (ignoreCase && char.ToUpperInvariant(*l) == char.ToUpperInvariant(*r))))
                {
                    commonChars++;
                    l++;
                    r++;
                }
            }

            return commonChars;
        }

        /// <summary>
        /// Get the common path length from the start of the string.
        /// </summary>
        internal static int GetCommonPathLength(string first, string second, bool ignoreCase)
        {
            int commonChars = EqualStartingCharacterCount(first, second, ignoreCase: ignoreCase);

            // If nothing matches
            if (commonChars == 0)
                return commonChars;

            // Or we're a full string and equal length or match to a separator
            if (commonChars == first.Length
                && (commonChars == second.Length || IsDirectorySeparator(second[commonChars])))
                return commonChars;

            if (commonChars == second.Length && IsDirectorySeparator(first[commonChars]))
                return commonChars;

            // It's possible we matched somewhere in the middle of a segment e.g. C:\Foodie and C:\Foobar.
            while (commonChars > 0 && !IsDirectorySeparator(first[commonChars - 1]))
                commonChars--;

            return commonChars;
        }

        /// <summary>
        /// Returns true if the path ends in a directory separator.
        /// </summary>
        internal static bool EndsInDirectorySeparator(ReadOnlySpan<char> path) =>
            path.Length > 0 && IsDirectorySeparator(path[path.Length - 1]);

        public static string GetRelativePath(string relativeTo, string path)
        {
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
            {
                return GetRelativePath(relativeTo, path, StringComparison.Ordinal);
            }
            else
            {
                return GetRelativePath(relativeTo, path, StringComparison.OrdinalIgnoreCase);
            }
        }

        private static string GetRelativePath(string relativeTo, string path, StringComparison comparisonType)
        {
            relativeTo = System.IO.Path.GetFullPath(relativeTo);
            path = System.IO.Path.GetFullPath(path);

            // Need to check if the roots are different- if they are we need to return the "to" path.
            if (!AreRootsEqual(relativeTo, path, comparisonType))
                return path;

            int commonLength = GetCommonPathLength(relativeTo, path, ignoreCase: comparisonType == StringComparison.OrdinalIgnoreCase);

            // If there is nothing in common they can't share the same root, return the "to" path as is.
            if (commonLength == 0)
                return path;

            // Trailing separators aren't significant for comparison
            int relativeToLength = relativeTo.Length;
            if (EndsInDirectorySeparator(relativeTo.AsSpan()))
                relativeToLength--;

            bool pathEndsInSeparator = EndsInDirectorySeparator(path.AsSpan());
            int pathLength = path.Length;
            if (pathEndsInSeparator)
                pathLength--;

            // If we have effectively the same path, return "."
            if (relativeToLength == pathLength && commonLength >= relativeToLength) return ".";

            // We have the same root, we need to calculate the difference now using the
            // common Length and Segment count past the length.
            //
            // Some examples:
            //
            //  C:\Foo C:\Bar L3, S1 -> ..\Bar
            //  C:\Foo C:\Foo\Bar L6, S0 -> Bar
            //  C:\Foo\Bar C:\Bar\Bar L3, S2 -> ..\..\Bar\Bar
            //  C:\Foo\Foo C:\Foo\Bar L7, S1 -> ..\Bar

            var sb = new StringBuilder(260);
            sb.EnsureCapacity(Math.Max(relativeTo.Length, path.Length));

            // Add parent segments for segments past the common on the "from" path
            if (commonLength < relativeToLength)
            {
                sb.Append("..");

                for (int i = commonLength + 1; i < relativeToLength; i++)
                {
                    if (IsDirectorySeparator(relativeTo[i]))
                    {
                        sb.Append(System.IO.Path.DirectorySeparatorChar);
                        sb.Append("..");
                    }
                }
            }
            else if (IsDirectorySeparator(path[commonLength]))
            {
                // No parent segments and we need to eat the initial separator
                //  (C:\Foo C:\Foo\Bar case)
                commonLength++;
            }

            // Now add the rest of the "to" path, adding back the trailing separator
            int differenceLength = pathLength - commonLength;
            if (pathEndsInSeparator)
                differenceLength++;

            if (differenceLength > 0)
            {
                if (sb.Length > 0)
                {
                    sb.Append(System.IO.Path.DirectorySeparatorChar);
                }

                sb.Append(path.Substring(commonLength, differenceLength));
            }

            return sb.ToString();
        }

    }
}
