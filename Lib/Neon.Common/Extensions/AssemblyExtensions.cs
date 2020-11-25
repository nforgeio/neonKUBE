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
        /// <summary>
        /// Returns an eumlated static file system that includes some or all of an assembly's
        /// embedded resources.  This method returns the root <see cref="IStaticDirectory"/>
        /// for the file system.
        /// </summary>
        /// <param name="assembly">The target assembly.</param>
        /// <param name="root">
        /// Optionally specifies the Linux-style path to the directory desired to be extracted
        /// as the root directory.
        /// </param>
        /// <returns>The root <see cref="IStaticDirectory"/>.</returns>
        /// <remarks>
        /// <para>
        /// This method maps Linux style paths (using forward not back slashes) to embedded
        /// resources in the assembly.  Resources in .NET projects are embedded and named
        /// like:
        /// </para>
        /// <example>
        /// ASSEMBLY-NAME [ "." PATH ] "." RESOURCE-FILENAME
        /// </example>
        /// <para>
        /// where <b>ASSEMBLY-NAME</b> is the name of the source assembly, <b>PATH</b> optionally
        /// specifies the path to the resource and <b>RESOURCE-FILENAME</b> specifies the name
        /// of the resource file.
        /// </para>
        /// <para>
        /// When a .NET project is built, any source files with build actions set to <b>Embedded Resource</b>
        /// will be included in the assembly using the naming convention described above.  The 
        /// <b>RESOURCE-FILENAME</b> will be set to the source resource file's name and <b>PATH</b>
        /// will be set to the relative path from the project file to the source resource file,
        /// but with any forward or back slashes converted to periods <b>[.]</b>.  So if your project
        /// is structured like:
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
        /// the following resources embedded in your project assembly:
        /// </para>
        /// <code>
        /// my-project.top-level.txt
        /// my-project.resources.resource1.dat
        /// my-project.resources.resource2.dat
        /// my-project.resources.tests.test1.txt
        /// my-project.resources.tests.test2.txt
        /// my-project.resources.samples.sample1.txt
        /// my-project.resources.samples.sample2.txt
        /// </code>
        /// <para>
        /// Calling <c>GetStaticFileSystem()</c> on your project assembly will return a <see cref="IStaticDirectory"/>
        /// with a directory structure holding all of the resources.  The paths are mapped from the
        /// resource names by removing the project name and then converting any dots <b>[.]</b> 
        /// for the last into forward slashes as required.  So the directory structure returned
        /// will effectively match where the original source files were. 
        /// </para>
        /// <code>
        /// /
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
        /// </remarks>
        public static IStaticDirectory GetStaticDirectory(this Assembly assembly, string root = null)
        {
            throw new NotImplementedException();
        }
    }
}
