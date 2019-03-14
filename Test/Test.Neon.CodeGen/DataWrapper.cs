//-----------------------------------------------------------------------------
// FILE:	    DataWrapper.cs
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
    /// Used to wrap a dynamically generated and compiled data model
    /// class for testing purposes.
    /// </summary>
    public class DataWrapper
    {
        private object  instance;
        private Type    instanceType;

        /// <summary>
        /// Constructs an instance with uninitialized properties.
        /// </summary>
        /// <param name="type">The target type.</param>
        public DataWrapper(Type type)
        {
            instance = Activator.CreateInstance(type);
            instanceType = type;

            if (instance == null)
            {
                throw new TypeLoadException($"Cannot instantiate type: {type.FullName}");
            }
        }

        /// <summary>
        /// Constructs an instance initialized from JSON text.
        /// </summary>
        /// <param name="type">The target type.</param>
        /// <param name="jsonText">The JSON text.</param>
        public DataWrapper(Type type, string jsonText)
        {
            Covenant.Requires<ArgumentNullException>(type != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrWhiteSpace(jsonText));

            var fromMethod = type.GetMethod("From", new Type[] { typeof(string) });

            instance     = fromMethod.Invoke(null, new object[] { jsonText });
            instanceType = type;

            if (instance == null)
            {
                throw new TypeLoadException($"Cannot instantiate type: {type.FullName}");
            }
        }

        /// <summary>
        /// Constructs an instance initialized from a <see cref="JObject"/>.
        /// </summary>
        /// <param name="type">The target type.</param>
        /// <param name="jObject">The <see cref="JObject"/>.</param>
        public DataWrapper(Type type, JObject jObject)
        {
            Covenant.Requires<ArgumentNullException>(type != null);
            Covenant.Requires<ArgumentNullException>(jObject != null);

            var fromMethod = type.GetMethod("From", new Type[] { typeof(JObject) });

            instance     = fromMethod.Invoke(null, new object[] { jObject });
            instanceType = type;

            if (instance == null)
            {
                throw new TypeLoadException($"Cannot instantiate type: {type.FullName}");
            }
        }

        /// <summary>
        /// Serializes the data model as JSON.
        /// </summary>
        /// <param name="indented">Optionally format the JSON output.</param>
        /// <returns>The JSON text.</returns>
        public string ToString(bool indented = false)
        {
            if (indented)
            {
                var method = instanceType.GetMethod("ToString", new Type[] { typeof(bool) });

                return (string)method.Invoke(instance, new object[] { indented });
            }
            else
            {
                var method = instanceType.GetMethod("ToString", new Type[] { });

                return (string)method.Invoke(instance, null);
            }
        }

        /// <summary>
        /// Accesses the wrapped data model's properties.
        /// </summary>
        /// <param name="propertyName">The property name.</param>
        public object this[string propertyName]
        {
            get
            {
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(propertyName));

                var property = instanceType.GetProperty(propertyName);

                if (property == null)
                {
                    throw new KeyNotFoundException($"Property [{propertyName}] was not found.");
                }

                return property.GetValue(instance);
            }

            set
            {
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(propertyName));

                var property = instanceType.GetProperty(propertyName);

                if (property == null)
                {
                    throw new KeyNotFoundException($"Property [{propertyName}] was not found.");
                }

                property.SetValue(instance, value);
            }
        }
    }
}
