//-----------------------------------------------------------------------------
// FILE:	    AssemblyExtensions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.IO;

namespace System
{
    /// <summary>
    /// Implements custom <see cref="Assembly"/> extension methods.
    /// </summary>
    public static class AssemblyExtensions
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Used to emulate resource directories.
        /// </summary>
        private class StaticResourceDirectory : StaticDirectoryBase
        {
            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="root">The root directory or <c>null</c> if this is the root.</param>
            /// <param name="parent">The parent directory or <c>null</c> for the root directory.</param>
            /// <param name="name">The directory name (this must be <c>null</c> for the root directory.</param>
            public StaticResourceDirectory(StaticResourceDirectory root, StaticResourceDirectory parent, string name)
                : base(root, parent, name)
            {
            }

            /// <summary>
            /// Adds a subdirectory if it doesn't already exist.
            /// </summary>
            /// <param name="directory">The child resource directory.</param>
            /// <returns>The existing <see cref="StaticResourceDirectory"/> or <paramref name="directory"/> if it was added</returns>
            internal StaticResourceDirectory AddDirectory(StaticResourceDirectory directory)
            {
                Covenant.Requires<ArgumentNullException>(directory != null, nameof(directory));

                return (StaticResourceDirectory)base.AddDirectory(directory);
            }

            /// <summary>
            /// Adds a file.
            /// </summary>
            /// <param name="file">The resource file.</param>
            internal void AddFile(StaticResourceFile file)
            {
                Covenant.Requires<ArgumentNullException>(file != null, nameof(file));

                base.AddFile(file);
            }
        }

        /// <summary>
        /// Wraps an embedded resource so it can be included in a static file system.
        /// </summary>
        private class StaticResourceFile : StaticFileBase
        {
            private Assembly    assembly;
            private string      resourceName;

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="path">The virtual path of the file within the file system.</param>
            /// <param name="assembly">The source assembly.</param>
            /// <param name="resourceName">The full name of the resource within the assembly.</param>
            public StaticResourceFile(string path, Assembly assembly, string resourceName)
                : base(path)
            {
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(path), nameof(path));
                Covenant.Requires<ArgumentNullException>(assembly != null, nameof(assembly));
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(resourceName), nameof(resourceName));

                this.assembly     = assembly;
                this.resourceName = resourceName;
            }

            /// <inheritdoc/>
            public override TextReader OpenReader(Encoding encoding = null)
            {
                return new StreamReader(
                    assembly.GetManifestResourceStream(resourceName),
                    encoding ?? Encoding.UTF8,
                    bufferSize: 8192,
                    detectEncodingFromByteOrderMarks: true,
                    leaveOpen: false);
            }

            /// <inheritdoc/>
            public async override Task<TextReader> OpenReaderAsync(Encoding encoding = null)
            {
                return await Task.FromResult(OpenReader(encoding));
            }

            /// <inheritdoc/>
            public override Stream OpenStream()
            {
                return assembly.GetManifestResourceStream(resourceName);
            }

            /// <inheritdoc/>
            public async override Task<Stream> OpenStreamAsync()
            {
                return await Task.FromResult(assembly.GetManifestResourceStream(resourceName));
            }

            /// <inheritdoc/>
            public override byte[] ReadAllBytes()
            {
                using (var stream = OpenStream())
                {
                    return stream.ReadToEnd();
                }
            }

            /// <inheritdoc/>
            public async override Task<byte[]> ReadAllBytesAsync()
            {
                var stream = await OpenStreamAsync();

                using (stream)
                {
                    return await stream.ReadToEndAsync();
                }
            }

            /// <inheritdoc/>
            public override string ReadAllText(Encoding encoding = null)
            {
                using (var reader = OpenReader(encoding ?? Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }

            /// <inheritdoc/>
            public async override Task<string> ReadAllTextAsync(Encoding encoding = null)
            {
                var reader = await OpenReaderAsync(encoding ?? Encoding.UTF8);

                using (reader)
                {
                    return await reader.ReadToEndAsync();
                }
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        /// <summary>
        /// Returns an eumlated static file system that includes some or all of an assembly's
        /// embedded resources.  This method returns the root <see cref="IStaticDirectory"/>
        /// for the file system.
        /// </summary>
        /// <param name="assembly">The target assembly.</param>
        /// <param name="resourcePrefix">
        /// <para>
        /// Specifies the resource name prefix to be used to identify the embedded resources
        /// to be included in the static file system.  See the remarks for more information.
        /// </para>
        /// </param>
        /// <returns>The root <see cref="IStaticDirectory"/>.</returns>
        /// <remarks>
        /// <para>
        /// This method maps Linux style paths (using forward not back slashes) to embedded
        /// resources in the assembly.  Resources in .NET projects are embedded and named
        /// like:
        /// </para>
        /// <example>
        /// ASSEMBLY-NAMESPACE [ "." DIR ] "." RESOURCE-FILENAME
        /// </example>
        /// <para>
        /// where <b>ASSEMBLY-NAME</b> is the name of the source assembly, <b>DIR</b> optionally
        /// specifies the directoriea to the resource and <b>RESOURCE-FILENAME</b> specifies the name
        /// of the resource file.
        /// </para>
        /// <para>
        /// When a .NET project is built, any source files with build actions set to <b>Embedded Resource</b>
        /// will be included in the assembly using the naming convention described above.  The 
        /// <b>ASSEMBLY-NAMESPACE</b> will be set to the source projects default namespace and the <b>DIR</b>s
        /// will be set to the relative path from the project file to the source resource file.  For
        /// example, if your project is structured like:
        /// </para>
        /// <code>
        /// my-project/
        ///     my-project.csproj
        ///     
        ///     top-level.txt
        ///     resources/
        ///         resource1.dat
        ///         resource2.dat
        ///         tests/
        ///             test1.txt
        ///             test2.txt
        ///         samples/
        ///             sample1.txt
        ///             sample2.txt
        /// </code>
        /// <para>
        /// and it's default namespace is <b>company.mproject</b>, then the following resources embedded 
        /// in your project assembly:
        /// </para>
        /// <code>
        /// company.my-project.top-level.txt
        /// company.my-project.resources.resource1.dat
        /// company.my-project.resources.resource2.dat
        /// company.my-project.resources.tests.test1.txt
        /// company.my-project.resources.tests.test2.txt
        /// company.my-project.resources.samples.sample1.txt
        /// company.my-project.resources.samples.sample2.txt
        /// </code>
        /// <para>
        /// By default, calling <see cref="AssemblyExtensions.GetResourceFileSystem(Assembly, string)"/> on your project assembly 
        /// will return a <see cref="IStaticDirectory"/> with a directory structure holding all of the resources.  
        /// The paths are mapped from the resource names by converting any dots except for the last one into forward
        /// slashes. 
        /// </para>
        /// <code>
        /// /
        ///     company/
        ///         my-project/
        ///             top-level.txt
        ///             resources/
        ///                 resource1.dat
        ///                 resource2.dat
        ///                 tests/
        ///                     test1.txt
        ///                     test2.txt
        ///                 samples/
        ///                     sample1.txt
        ///                     sample2.txt
        /// </code>
        /// <para>
        /// You can also pass an optional resource name prefix so that only a subset of the resources
        /// are included in the file system.  For example, by passing <b>company.my-project.resources</b>,
        /// file system returned will look like:
        /// </para>
        /// <code>
        /// /
        ///     resource1.dat
        ///     resource2.dat
        ///     tests/
        ///         test1.txt
        ///         test2.txt
        ///     samples/
        ///         sample1.txt
        ///         sample2.txt
        /// </code>
        /// <para><b>RESOURCE NAMING AMBIGUITIES</b></para>
        /// <para>
        /// When creating the file system from resource names, it's possible to encounter situations
        /// where it's not possible to distingish between a a directory and a file name.  For example:
        /// </para>
        /// <code>
        /// company.my-project.resources.resource1.dat
        /// </code>
        /// <para>
        /// Here are some possibilities:
        /// </para>
        /// <list type="bullet">
        ///     <item>
        ///     path is <b>/company.my-project.resources.resource1</b> and the file name <b>dat</b>
        ///     </item>
        ///     <item>
        ///     path is <b>/company.my-project.resources</b> and the file name <b>resource1.dat</b>
        ///     </item>
        ///     <item>
        ///     path is <b>/company.my-project</b> and the file name <b>.resources.resource1.dat</b>
        ///     </item>
        /// </list>
        /// <para>
        /// There is really no way for this method to know what the original resource source files
        /// and directory paths were.  To resolve this, we're going to assume that resource file
        /// names include a file extension with a single dot and that any additional dots will form
        /// the file's parent directory path.  
        /// </para>
        /// <para>
        /// What this means is that your resource file names must include a file extension.  So, file 
        /// names like this are OK:
        /// </para>
        /// <code>
        /// schema.sql
        /// config.json
        /// </code>
        /// <para>
        /// You should avoid file names like:
        /// </para>
        /// <code>
        /// schema.1.sql
        /// </code>
        /// <para>
        /// and use something like a dash instead so that <b>schema</b> won't be considered to
        /// be part of the file's parent directory path:
        /// </para>
        /// <code>
        /// schema-1.sql
        /// </code>
        /// </remarks>
        public static IStaticDirectory GetResourceFileSystem(this Assembly assembly, string resourcePrefix = null)
        {
            if (!string.IsNullOrEmpty(resourcePrefix))
            {
                if (resourcePrefix.Last() != '.')
                {
                    resourcePrefix += '.';
                }
            }

            var pathToDirectory       = new Dictionary<string, StaticResourceDirectory>(StringComparer.InvariantCultureIgnoreCase);
            var resourceNames         = assembly.GetManifestResourceNames();
            var filteredResourceNames = string.IsNullOrEmpty(resourcePrefix)
                                            ? resourceNames
                                            : resourceNames
                                                  .Where(name => name.StartsWith(resourcePrefix) && name != resourcePrefix);
            // Add the root directory.

            var root = new StaticResourceDirectory(root: null, parent: null, name: string.Empty);

            pathToDirectory[root.Name] = root;

            foreach (var resourceName in filteredResourceNames)
            {
                // Split the resource name into the directory path and filename parts.

                var trimmedName = resourceName.Substring(resourcePrefix?.Length ?? 0);
                var pos         = trimmedName.LastIndexOf('.');
                var path        = (string)null;
                var filename    = (string)null;

                if (pos == -1)
                {
                    // Special case of a file without an extension.  This can happen
                    // when a resource prefix is used.  This can only happen at the
                    // file system root directory.

                    path     = "/";
                    filename = trimmedName;
                }
                else
                {
                    // The second dot from the end will indicate start of the file name
                    // when there is a second dot.

                    pos = trimmedName.LastIndexOf('.', pos - 1);

                    if (pos != -1)
                    {
                        path     = "/" + trimmedName.Substring(0, pos).Replace('.', '/');
                        filename = trimmedName.Substring(pos + 1);
                    }
                    else
                    {
                        path     = "/";
                        filename = trimmedName;
                    }
                }

                if (!pathToDirectory.TryGetValue(path, out var directory))
                {
                    // Create the new directory, ensuring that any parent
                    // directories exist as well.

                    var parent = root;

                    foreach (var directoryName in path.Split('/').Skip(1))
                    {
                        if (!pathToDirectory.TryGetValue(directoryName, out directory))
                        {
                            directory = parent.AddDirectory(new StaticResourceDirectory(root, parent, directoryName));
                        }

                        parent = directory;
                    }
                }

                directory.AddFile(new StaticResourceFile(LinuxPath.Combine(path, filename), assembly, resourceName));
            }

            return root;
        }
    }
}
