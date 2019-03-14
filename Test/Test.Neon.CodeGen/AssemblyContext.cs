//-----------------------------------------------------------------------------
// FILE:	    AssemblyContext.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

using Neon.CodeGen;
using Neon.Common;
using Neon.Xunit;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Xunit;

namespace TestCodeGen.CodeGen
{
    /// <summary>
    /// Implements assembly context for testing.
    /// </summary>
    public class AssemblyContext : AssemblyLoadContext, IDisposable
    {
        private string      defaultNamespace;
        private Assembly    assembly;
        private bool        isDisposed;

        /// <summary>
        /// Constructor. 
        /// </summary>
        /// <param name="defaultNamespace">The default namespace to be used for instanting types.</param>
        /// <param name="assemblyStream">The stream holding the assembly to be loaded.</param>
        public AssemblyContext(string defaultNamespace, Stream assemblyStream)
            : base(isCollectible: true)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(defaultNamespace));
            Covenant.Requires<ArgumentNullException>(assemblyStream != null);

            this.defaultNamespace = defaultNamespace;
            this.assembly         = LoadFromStream(assemblyStream);
        }

        public void Dispose()
        {
            if (!isDisposed)
            {
                isDisposed = true;
                Unload();
            }
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            return null;
        }

        /// <summary>
        /// Creates an uni9n <see cref="DataWrapper"/> instance around a new instance
        /// of the named type.
        /// </summary>
        /// <typeparam name="T">The type as defined in the </typeparam>
        /// <param name="typeName">The data model type as defined in the source assembly.</param>
        /// <returns>The new <see cref="DataWrapper"/>.</returns>
        public DataWrapper CreateDataWrapper<T>()
        {
            var sourceType = typeof(T);

            return new DataWrapper(assembly, $"{defaultNamespace}.{sourceType.Name}");
        }
    }
}
