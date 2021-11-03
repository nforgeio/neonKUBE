//-----------------------------------------------------------------------------
// FILE:	    CSharpHelper.cs
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
using System.Text;
using System.Threading.Tasks;

using Basic.Reference.Assemblies;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

using Neon.Common;

namespace Neon.CSharp
{
    /// <summary>
    /// C# dynamic compilation related utilities.
    /// </summary>
    public static class CSharpHelper
    {
        /// <summary>
        /// Returns the reference assembles for the current .NET runtime environment.  These are
        /// required when dynamically compiling C# code.
        /// </summary>
        /// <exception cref="NotSupportedException">Thrown when the current runtime environment is not recognized or supported.</exception>
        public static IEnumerable<PortableExecutableReference> RuntimeReferenceAssemblies
        {
            get
            {
                var frameworkVersion = NeonHelper.FrameworkVersion;

                switch (NeonHelper.Framework)
                {
                    case NetFramework.NetFramework:

                        if (frameworkVersion.Major == 4)
                        {
                            if (frameworkVersion.Minor == 6)
                            {
                                return ReferenceAssemblies.Net461;
                            }
                            else if (frameworkVersion.Minor >= 7)
                            {
                                return ReferenceAssemblies.Net472;
                            }
                        }
                        break;

                    case NetFramework.Core:

                        return ReferenceAssemblies.NetCoreApp31;

                    case NetFramework.Net:

                        switch (frameworkVersion.Major)
                        {
                            case 5:

                                return ReferenceAssemblies.Net50;

                            case 6:

                                return ReferenceAssemblies.Net60;
                        }
                        break;
                }

                throw new NotSupportedException($"Framework[{NeonHelper.FrameworkDescription}] is not currently supported.");
            }
        }

        /// <summary>
        /// Compiles C# source code into an assembly.
        /// </summary>
        /// <param name="source">The C# source code.</param>
        /// <param name="assemblyName">The generated assembly name.</param>
        /// <param name="referenceHandler">Called to manage metadata/assembly references (see remarks).</param>
        /// <param name="compilerOptions">Optional compilation options.  This defaults to building a release assembly.</param>
        /// <returns>The compiled assembly as a <see cref="MemoryStream"/>.</returns>
        /// <exception cref="CompilerErrorException">Thrown for compiler errors.</exception>
        /// <remarks>
        /// <para>
        /// By default, this method will compile the assembly with the standard 
        /// reference assemblies for the currently executing runtime.
        /// </para>
        /// <para>
        /// You may customize these by passing a <paramref name="referenceHandler"/>
        /// action.  This is passed the list of <see cref="MetadataReference"/> instances.
        /// You can add or remove references as required.  The easiest way to add
        /// a reference is to use type reference like:
        /// </para>
        /// <code>
        /// using Microsoft.CodeAnalysis;
        /// 
        /// ...
        /// 
        /// var source   = "public class Foo {}";
        /// var assembly = CSharpHelper.Compile(source, "my-assembly",
        ///     references =>
        ///     {
        ///         references.Add(typeof(MyClass));    // Adds the assembly containing MyClass.
        ///     });
        /// </code>
        /// </remarks>
        public static MemoryStream Compile(
            string                          source, 
            string                          assemblyName, 
            Action<MetadataReferences>      referenceHandler = null,
            CSharpCompilationOptions        compilerOptions  = null)
        {
            Covenant.Requires<ArgumentNullException>(source != null, nameof(source));

            var syntaxTree = CSharpSyntaxTree.ParseText(source);
            var references = new MetadataReferences();

            // Add assembly references.

            references.AddRange(CSharpHelper.RuntimeReferenceAssemblies);
            references.Add(typeof(NeonHelper));

            // Allow the caller to add references.

            referenceHandler?.Invoke(references);

            if (compilerOptions == null)
            {
                compilerOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release);
            }

            var compilation = CSharpCompilation.Create(assemblyName, new[] { syntaxTree }, references, compilerOptions);
            var dllStream   = new MemoryStream();

            using (var pdbStream = new MemoryStream())
            {
                var emitted = compilation.Emit(dllStream, pdbStream);

                if (!emitted.Success)
                {
                    throw new CompilerErrorException(emitted.Diagnostics);
                }
            }

            dllStream.Position = 0;

            return dllStream;
        }
    }
}
