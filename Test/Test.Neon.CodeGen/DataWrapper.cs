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

namespace TestCodeGen
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
            instance     = Activator.CreateInstance(type);
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

            try
            {
                var createFromMethod = type.GetMethod("CreateFrom", new Type[] { typeof(string) });

                instance     = createFromMethod.Invoke(null, new object[] { jsonText });
                instanceType = type;

                if (instance == null)
                {
                    throw new TypeLoadException($"Cannot instantiate type: {type.FullName}");
                }
            }
            catch (TargetInvocationException e)
            {
                if (e.InnerException != null)
                {
                    throw e.InnerException;
                }
                else
                {
                    throw;
                }
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

            try
            {
                var createFromMethod = type.GetMethod("CreateFrom", new Type[] { typeof(JObject) });

                instance     = createFromMethod.Invoke(null, new object[] { jObject });
                instanceType = type;

                if (instance == null)
                {
                    throw new TypeLoadException($"Cannot instantiate type: {type.FullName}");
                }
            }
            catch (TargetInvocationException e)
            {
                if (e.InnerException != null)
                {
                    throw e.InnerException;
                }
                else
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// Returns the wrapped data model instance.
        /// </summary>
        public object __Instance => instance;

        /// <summary>
        /// Returns the <see cref="JObject"/> backing the wrapped class.
        /// </summary>
        public JObject JObject => (JObject)instanceType.GetProperty("__JObject", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);

        /// <summary>
        /// Accesses the wrapped data model's properties.
        /// </summary>
        /// <param name="propertyName">The property name.</param>
        public object this[string propertyName]
        {
            get
            {
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(propertyName));

                try
                {
                    var property = instanceType.GetProperty(propertyName);

                    if (property == null)
                    {
                        throw new KeyNotFoundException($"Property [{propertyName}] was not found.");
                    }

                    return property.GetValue(instance);
                }
                catch (TargetInvocationException e)
                {
                    if (e.InnerException != null)
                    {
                        throw e.InnerException;
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            set
            {
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(propertyName));

                try
                {
                    var property = instanceType.GetProperty(propertyName);

                    if (property == null)
                    {
                        throw new KeyNotFoundException($"Property [{propertyName}] was not found.");
                    }

                    property.SetValue(instance, value);
                }
                catch (TargetInvocationException e)
                {
                    if (e.InnerException != null)
                    {
                        throw e.InnerException;
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Serializes the data model as JSON.
        /// </summary>
        /// <param name="indented">Optionally format the JSON output.</param>
        /// <returns>The JSON text.</returns>
        public string ToString(bool indented = false)
        {
            try
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
            catch (TargetInvocationException e)
            {
                if (e.InnerException != null)
                {
                    throw e.InnerException;
                }
                else
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// Serializes the data model as a <see cref="JObjects"/>.
        /// </summary>
        /// <param name="indented">Optionally format the JSON output.</param>
        /// <returns>The JSON text.</returns>
        public JObject ToJObject()
        {
            try
            {
                var method = instanceType.GetMethod("ToJObject", new Type[] { });

                return (JObject)method.Invoke(instance, new object[] { });
            }
            catch (TargetInvocationException e)
            {
                if (e.InnerException != null)
                {
                    throw e.InnerException;
                }
                else
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// Returns a deep clone of the data model.
        /// </summary>
        /// <returns>The new data <see cref="DataWrapper"/> with the cloned instance.</returns>
        public DataWrapper DeepClone()
        {
            try
            {
                var cloneMethod     = instanceType.GetMethod("DeepClone", new Type[] { });
                var clone           = cloneMethod.Invoke(instance, new object[] { });
                var toJObjectMethod = instanceType.GetMethod("ToJObject", new Type[] { });
                var jObject         = (JObject)toJObjectMethod.Invoke(instance, new object[] { });

                return new DataWrapper(instanceType, jObject);
            }
            catch (TargetInvocationException e)
            {
                if (e.InnerException != null)
                {
                    throw e.InnerException;
                }
                else
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// Compares one wrapped data model with another for equality.
        /// </summary>
        /// <param name="obj">The other instance (or <c>null</c>).</param>
        /// <returns><c>true</c> if the data models report being equal.</returns>
        public override bool Equals(object obj)
        {
            var method = instanceType.GetMethod("Equals", new Type[] { typeof(object) });

            if (obj == null)
            {
                return (bool)method.Invoke(instance, new object[] { obj });
            }

            var instanceProperty = obj.GetType().GetProperty("__Instance");

            if (instanceProperty == null)
            {
                // [obj] is not a wrapped data model, so we'll
                // just pass it through to Equals().

                return (bool)method.Invoke(instance, new object[] { obj });
            }

            var otherInstance = instanceProperty.GetValue(obj);

            return (bool)method.Invoke(instance, new object[] { otherInstance });
        }

        /// <summary>
        /// Uses the generated data model's <b>==</b> operator override to compare two
        /// wrapped instances.
        /// </summary>
        /// <typeparam name="T">The source data type as defined in the within the unit test assembly.</typeparam>
        /// <param name="value1">The first wrapped instance (or <c>null</c>).</param>
        /// <param name="value2">The second wrapped instance (or <c>null</c>).</param>
        /// <returns><c>true</c> if the instances are equal.</returns>
        public static bool Equals<T>(DataWrapper value1, DataWrapper value2)
        {
            var sourceType = typeof(T);
            var targetType = AssemblyContext.Current.Assembly.GetType($"{AssemblyContext.Current.DefaultNamespace}.{sourceType.Name}");

            if (targetType == null)
            {
                throw new TypeLoadException($"Cannot find type: {AssemblyContext.Current.DefaultNamespace}.{sourceType.Name}");
            }

            var equalsOperator = targetType.GetMethod("op_Equality", new Type[] { targetType, targetType });
            var instance1      = value1 != null ? value1.instance : null;
            var instance2      = value2 != null ? value2.instance : null;

            return (bool)equalsOperator.Invoke(null, new object[] { instance1, instance2 });
        }


        /// <summary>
        /// Uses the generated data model's <b>!=</b> operator override to compare two
        /// wrapped instances.
        /// </summary>
        /// <typeparam name="T">The source data type as defined in the within the unit test assembly.</typeparam>
        /// <param name="value1">The first wrapped instance (or <c>null</c>).</param>
        /// <param name="value2">The second wrapped instance (or <c>null</c>).</param>
        /// <returns><c>true</c> if the instances are not equal.</returns>
        public static bool NotEquals<T>(DataWrapper value1, DataWrapper value2)
        {
            var sourceType = typeof(T);
            var targetType = AssemblyContext.Current.Assembly.GetType($"{AssemblyContext.Current.DefaultNamespace}.{sourceType.Name}");

            if (targetType == null)
            {
                throw new TypeLoadException($"Cannot find type: {AssemblyContext.Current.DefaultNamespace}.{sourceType.Name}");
            }

            var notEqualsOperator = targetType.GetMethod("op_Inequality", new Type[] { targetType, targetType });
            var instance1         = value1 != null ? value1.instance : null;
            var instance2         = value2 != null ? value2.instance : null;

            return (bool)notEqualsOperator.Invoke(null, new object[] { instance1, instance2 });
        }

        /// <summary>
        /// Returns the hash code computed by the wrapped data model.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            var method = instanceType.GetMethod("GetHashCode", new Type[] { });

            try
            {
                return (int)method.Invoke(instance, new object[] { });
            }
            catch (TargetInvocationException e)
            {
                if (e.InnerException != null)
                {
                    throw e.InnerException;
                }
                else
                {
                    throw;
                }
            }
        }
    }
}
