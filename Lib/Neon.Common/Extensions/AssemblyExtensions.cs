//-----------------------------------------------------------------------------
// FILE:	    AssemblyExtensions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
        /// Wraps an embedded resource so it can be included in a static file system.
        /// </summary>
        private class StaticResourceFile : StaticFileBase
        {
            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="path">The virtual path of the file within the file system.</param>
            /// <param name="resourceName">The full name of the resource in assembly manifest.</param>
            public StaticResourceFile(string path, string resourceName)
                : base(path)
            {
            }

            /// <inheritdoc/>
            public override TextReader OpenReader(Encoding encoding = null)
            {
                throw new NotImplementedException();
            }

            /// <inheritdoc/>
            public override Task<TextReader> OpenReaderAsync(Encoding encoding = null)
            {
                throw new NotImplementedException();
            }

            /// <inheritdoc/>
            public override Stream OpenStream()
            {
                throw new NotImplementedException();
            }

            /// <inheritdoc/>
            public override Task<Stream> OpenStreamAsync()
            {
                throw new NotImplementedException();
            }

            /// <inheritdoc/>
            public override byte[] ReadAllBytes()
            {
                throw new NotImplementedException();
            }

            /// <inheritdoc/>
            public override Task<byte[]> ReadAllBytesAsync()
            {
                throw new NotImplementedException();
            }

            /// <inheritdoc/>
            public override string ReadAllText(Encoding encoding = null)
            {
                throw new NotImplementedException();
            }

            /// <inheritdoc/>
            public override Task<string> ReadAllTextAsync(Encoding encoding = null)
            {
                throw new NotImplementedException();
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
        /// </remarks>
        public static IStaticDirectory GetResourceFileSystem(this Assembly assembly, string resourcePrefix = null)
        {
            var resourceNames = assembly.GetManifestResourceNames();

            return null;
        }
    }
}
