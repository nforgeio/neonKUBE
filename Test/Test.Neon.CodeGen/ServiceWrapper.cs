//-----------------------------------------------------------------------------
// FILE:	    ServiceWrapper.cs
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
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.CodeGen;
using Neon.Common;
using Neon.Diagnostics;
using Neon.Xunit;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Xunit;

namespace TestCodeGen
{
    /// <summary>
    /// Used to wrap a dynamically generated and compiled service
    /// client class for testing purposes.
    /// </summary>
    public sealed class ServiceWrapper : IDisposable
    {
        private object              instance;
        private Type                instanceType;
        private CancellationToken   defaultCancellationToken;
        private object              defaultLogActivity;

        /// <summary>
        /// Constructs an instance with uninitialized properties.
        /// </summary>
        /// <param name="type">The target type.</param>
        /// <param name="baseAddress">The base URI for the target service.</param>
        /// <param name="context">The parent assembly context.</param>
        public ServiceWrapper(Type type, string baseAddress, AssemblyContext context)
        {
            Covenant.Requires<ArgumentNullException>(type != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(baseAddress));

            instance     = Activator.CreateInstance(type, new object[] { null, false });
            instanceType = type;

            if (instance == null)
            {
                throw new TypeLoadException($"Cannot instantiate type: {type.FullName}");
            }

            var baseAddressProperty = instanceType.GetProperty("BaseAddress");

            if (baseAddressProperty == null)
            {
                throw new ArgumentException($"Cannot find method: BaseAddress");
            }

            baseAddressProperty.SetValue(instance, new Uri(baseAddress));

            // Set some default arguments.  Note that some of these
            // need to be obtained from the generated assembly.

            this.defaultCancellationToken = default(CancellationToken);
            this.defaultLogActivity       = Activator.CreateInstance(context.NeonCommonAssembly.GetType(typeof(LogActivity).FullName));
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            var method = instanceType.GetMethod("Dispose");

            if (method == null)
            {
                throw new ArgumentException($"Cannot find method: Dispose()");
            }

            method.Invoke(instance, new object[0]);
        }

        /// <summary>
        /// Calls a named <c>void</c> method.
        /// </summary>
        /// <param name="methodName">The method name.</param>
        /// <param name="args">The arguments.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <remarks>
        /// <note>
        /// <b>IMPORTANT:</b> This method doesn't handle method overloading 
        /// so you'll need to ensure that all methods have unique names.
        /// </note>
        /// </remarks>
        public async Task CallAsync(string methodName, params object[] args)
        {
            if (!methodName.EndsWith("Async"))
            {
                methodName += "Async";
            }

            var method = instanceType.GetMethod(methodName);

            if (method == null)
            {
                throw new ArgumentException($"Cannot find method: {methodName}()");
            }

            // We need to pass the default cancellation token and log activity values too.

            var argList = new List<object>();

            foreach (var arg in args)
            {
                argList.Add(arg);
            }

            argList.Add(defaultCancellationToken);
            argList.Add(defaultLogActivity);

            await (Task)method.Invoke(instance, argList.ToArray());
        }

        /// <summary>
        /// Calls a named method that returns a specific type.
        /// </summary>
        /// <typeparam name="TResult">The method result type.</typeparam>
        /// <param name="methodName">The method name.</param>
        /// <param name="args">The arguments.</param>
        /// <returns>The <typeparamref name="TResult"/> method result.</returns>
        /// <remarks>
        /// <note>
        /// <b>IMPORTANT:</b> This method doesn't handle method overloading 
        /// so you'll need to ensure that all methods have unique names.
        /// </note>
        /// </remarks>
        public async Task<TResult> CallAsync<TResult>(string methodName, params object[] args)
        {
            var method = instanceType.GetMethod(methodName);

            if (method == null)
            {
                throw new ArgumentException($"Cannot find method: {methodName}()");
            }

            return await (Task<TResult>)method.Invoke(instance, args);
        }
    }
}
