/*
    CSharpEditor - A C# source code editor with syntax highlighting, intelligent
    code completion and real-time compilation error checking.
    Copyright (C) 2021  Giorgio Bianchini
 
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, version 3.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace CSharpEditor
{
    /// <summary>
    /// This class represents a cached <see cref="MetadataReference"/>. When an instance of this class is created using the <see cref="CreateFromFile(string, string)"/> method,
    /// we check whether a <see cref="CachedMetadataReference"/> to the same file has already been created and, in that case, return a reference to that object, rathern than creating a new one.
    /// </summary>
    public class CachedMetadataReference
    {
        private static Dictionary<string, CachedMetadataReference> CachedReferences = new Dictionary<string, CachedMetadataReference>();

        private MetadataReference Reference { get; }

        /// <summary>
        /// Creates a new <see cref="CachedMetadataReference"/> wrapping the specified <paramref name="reference"/>.
        /// </summary>
        /// <param name="reference">The <see cref="MetadataReference"/> wrap in a new <see cref="CachedMetadataReference"/>.</param>
        public CachedMetadataReference(MetadataReference reference)
        {
            this.Reference = reference;
        }

        /// <summary>
        /// Creates a new <see cref="CachedMetadataReference"/> from an assembly file (optionally including the XML documentation).
        /// </summary>
        /// <param name="path">The path to the assembly file.</param>
        /// <param name="xmlDocumentationPath">The path to the XML documentation file for the assembly.</param>
        /// <returns>
        /// If a <see cref="CachedMetadataReference"/> has already been created from the same assembly file and the same XML documentation, a reference to the previously created object.
        /// Otherwise, a new <see cref="CachedMetadataReference"/> wrapping a <see cref="MetadataReference"/> created from the specified assembly file.
        /// </returns>
        public static CachedMetadataReference CreateFromFile(string path, string xmlDocumentationPath = null)
        {
            string key = path + ":*:" + xmlDocumentationPath;

            if (CachedReferences.TryGetValue(key, out CachedMetadataReference cached))
            {
                return cached;
            }
            else
            {
                MetadataReference metadataReference = MetadataReference.CreateFromFile(path, documentation: XmlDocumentationProvider.CreateFromFile(xmlDocumentationPath));
                CachedMetadataReference reference = new CachedMetadataReference(metadataReference);
                CachedReferences.Add(key, reference);
                return reference;
            }
        }

        /// <summary>
        /// Converts a <see cref="CachedMetadataReference"/> into a <see cref="MetadataReference"/>.
        /// </summary>
        /// <param name="reference">The <see cref="CachedMetadataReference"/> to convert.</param>
        public static implicit operator MetadataReference(CachedMetadataReference reference)
        {
            return reference.Reference;
        }
    }
}
