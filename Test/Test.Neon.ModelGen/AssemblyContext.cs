//-----------------------------------------------------------------------------
// FILE:	    AssemblyContext.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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

using Neon.ModelGen;
using Neon.Common;
using Neon.Data;
using Neon.Xunit;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Xunit;

namespace TestModelGen
{
    /// <summary>
    /// Implements assembly context for testing.
    /// </summary>
    public class AssemblyContext : AssemblyLoadContext, IDisposable
    {
        //---------------------------------------------------------------------
        // Static members

        public static AssemblyContext Current { get; private set; }

        //---------------------------------------------------------------------
        // Instance members

        private bool isDisposed;

        /// <summary>
        /// Constructor. 
        /// </summary>
        /// <param name="defaultNamespace">The default namespace to be used for instanting types.</param>
        /// <param name="assemblyStream">The stream holding the assembly to be loaded.</param>
        public AssemblyContext(string defaultNamespace, Stream assemblyStream)
            : base(isCollectible: true)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(defaultNamespace), nameof(defaultNamespace));
            Covenant.Requires<ArgumentNullException>(assemblyStream != null, nameof(assemblyStream));
            Covenant.Assert(Current == null);

            AssemblyContext.Current = this;

            // We need the [Neon.Common] assembly.

            base.LoadFromAssemblyPath(typeof( IRoundtripData).Assembly.Location);

            // Load the assembly passed.

            this.DefaultNamespace = defaultNamespace;
            this.LoadedAssembly   = LoadFromStream(assemblyStream);

            // Also load the [Neon.Common] assembly.  This should be located
            // in the same directory as the running test assembly.

            var currentAssembly     = Assembly.GetExecutingAssembly();
            var currentAssemblyPath = currentAssembly.Location;

            this.NeonCommonAssembly = base.LoadFromAssemblyPath(Path.Combine(Path.GetDirectoryName(currentAssemblyPath), "Neon.Common.dll"));
        }

        public void Dispose()
        {
            if (!isDisposed)
            {
                Current = null;
                isDisposed = true;
                Unload();
            }
        }

        /// <summary>
        /// Returns the default namespace.
        /// </summary>
        public string DefaultNamespace { get; private set; }

        /// <summary>
        /// Returns the loaded assembly.
        /// </summary>
        public Assembly LoadedAssembly { get; private set; }

        /// <summary>
        /// Returns the loaded <b>Neon.Common</b> assembly.
        /// </summary>
        public Assembly NeonCommonAssembly { get; private set; }

        /// <summary>
        /// Required to implement [AssemblyLoadContext] but is never called.
        /// </summary>
        /// <param name="assemblyName"></param>
        /// <returns></returns>
        protected override Assembly Load(AssemblyName assemblyName)
        {
            return null;
        }

        /// <summary>
        /// Creates a unit <see cref="DataWrapper"/> instance around a new instance
        /// of the named type.
        /// </summary>
        /// <typeparam name="T">The source data type as defined in the within the unit test assembly.</typeparam>
        /// <returns>The new <see cref="DataWrapper"/>.</returns>
        public DataWrapper CreateDataWrapper<T>()
        {
            var sourceType = typeof(T);
            var targetType = LoadedAssembly.GetType($"{DefaultNamespace}.{sourceType.Name}");

            if (targetType == null)
            {
                throw new TypeLoadException($"Cannot find type: {DefaultNamespace}.{sourceType.Name}");
            }

            return new DataWrapper(targetType);
        }

        /// <summary>
        /// Creates a data wrapper from JSON text.
        /// </summary>
        /// <typeparam name="T">The source data type as defined in the within the unit test assembly.</typeparam>
        /// <param name="jsonText">The JSON text.</param>
        /// <returns>The new <see cref="DataWrapper"/>.</returns>
        public DataWrapper CreateDataWrapperFrom<T>(string jsonText)
        {
            var sourceType = typeof(T);
            var targetType = LoadedAssembly.GetType($"{DefaultNamespace}.{sourceType.Name}");

            if (targetType == null)
            {
                throw new TypeLoadException($"Cannot find type: {DefaultNamespace}.{sourceType.Name}");
            }

            return new DataWrapper(targetType, jsonText);
        }

        /// <summary>
        /// Creates a data wrapper from UTF-8 encoded JSON.
        /// </summary>
        /// <typeparam name="T">The source data type as defined in the within the unit test assembly.</typeparam>
        /// <param name="bytes">The JSON bytes.</param>
        /// <returns>The new <see cref="DataWrapper"/>.</returns>
        public DataWrapper CreateDataWrapperFrom<T>(byte[] bytes)
        {
            var sourceType = typeof(T);
            var targetType = LoadedAssembly.GetType($"{DefaultNamespace}.{sourceType.Name}");

            if (targetType == null)
            {
                throw new TypeLoadException($"Cannot find type: {DefaultNamespace}.{sourceType.Name}");
            }

            return new DataWrapper(targetType, bytes);
        }

        /// <summary>
        /// Creates a data wrapper from a <see cref="JObject"/>.
        /// </summary>
        /// <typeparam name="T">The source data type as defined in the within the unit test assembly.</typeparam>
        /// <param name="jObject">The <see cref="JObject"/>.</param>
        /// <returns>The new <see cref="DataWrapper"/>.</returns>
        public DataWrapper CreateDataWrapperFrom<T>(JObject jObject)
        {
            var sourceType = typeof(T);
            var targetType = LoadedAssembly.GetType($"{DefaultNamespace}.{sourceType.Name}");

            if (targetType == null)
            {
                throw new TypeLoadException($"Cannot find type: {DefaultNamespace}.{sourceType.Name}");
            }

            return new DataWrapper(targetType, jObject);
        }

        /// <summary>
        /// Creates a service wrapper>.
        /// </summary>
        /// <typeparam name="T">The source service type as defined in the within the unit test assembly.</typeparam>
        /// <param name="baseAddress">The base address to use for the created client.</param>
        /// <returns>The new <see cref="ServiceWrapper"/>.</returns>
        public ServiceWrapper CreateServiceWrapper<T>(string baseAddress)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(baseAddress), nameof(baseAddress));

            const string controllerSuffix = "Controller";

            var sourceType     = typeof(T);
            var clientTypeName = sourceType.Name;

            if (clientTypeName.EndsWith(controllerSuffix))
            {
                clientTypeName = clientTypeName.Substring(0, clientTypeName.Length - controllerSuffix.Length);
            }

            var targetType = LoadedAssembly.GetType($"{DefaultNamespace}.{clientTypeName}Client");

            if (targetType == null)
            {
                throw new TypeLoadException($"Cannot find type: {DefaultNamespace}.{sourceType.Name}");
            }

            return new ServiceWrapper(targetType, baseAddress, this);
        }
    }
}
