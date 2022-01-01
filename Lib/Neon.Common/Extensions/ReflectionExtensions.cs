//-----------------------------------------------------------------------------
// FILE:	    ReflectionExtensions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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

namespace System
{
    /// <summary>
    /// Reflection related extension methods.
    /// </summary>
    public static class ReflectionExtensions
    {
        //---------------------------------------------------------------------
        // System.Type:

        /// <summary>
        /// Determines whether a <see cref="System.Type"/> implements a specific interface.
        /// </summary>
        /// <typeparam name="TInterface">The required interface type.</typeparam>
        /// <param name="type">The type being tested.</param>
        /// <returns><c>true</c> if <paramref name="type"/> implements <paramref name="type"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="type"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown if <typeparamref name="TInterface"/> is not an <c>interface</c>.</exception>
        public static bool Implements<TInterface>(this Type type)
        {
            return Implements(type, typeof(TInterface));
        }

        /// <summary>
        /// Determines whether a <see cref="System.Type"/> implements a specific interface.
        /// </summary>
        /// <param name="type">The type being tested.</param>
        /// <param name="interfaceType">The interface type.</param>
        /// <returns><c>true</c> if <paramref name="type"/> implements <paramref name="type"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if either of <paramref name="type"/> or <paramref name="interfaceType"/> are <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="interfaceType"/> is not an <c>interface</c>.</exception>
        public static bool Implements(Type type, Type interfaceType)
        {
            Covenant.Requires<ArgumentNullException>(type != null, nameof(type));
            Covenant.Requires<ArgumentNullException>(interfaceType != null, nameof(interfaceType));
            Covenant.Requires<ArgumentException>(interfaceType.IsInterface, nameof(interfaceType), $"Type [{interfaceType.FullName}] is not an interface.");

            foreach (var @interface in type.GetInterfaces())
            {
                if (@interface == interfaceType)
                {
                    return true;
                }

                if (Implements(@interface, interfaceType))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether a type inherits from (or is) a specified base type.
        /// </summary>
        /// <typeparam name="T">Identifies the type we're matching.</typeparam>
        /// <param name="type">The type being tested.</param>
        /// <returns><c>true</c> if <paramref name="type"/> inherits or is <typeparamref name="T"/>.</returns>
        public static bool Is<T>(this Type type)
        {
            Covenant.Requires<ArgumentNullException>(type != null, nameof(type));

            return Is(type, typeof(T));
        }

        /// <summary>
        /// Determines whether a type inherits from (or is) a specified base type.
        /// </summary>
        /// <param name="type">The type being tested.</param>
        /// <param name="targetType">Identifies the type we're matching.</param>
        /// <returns><c>true</c> if <paramref name="type"/> inherits or is <paramref name="targetType"/>.</returns>
        public static bool Is(this Type type, Type targetType)
        {
            Covenant.Requires<ArgumentNullException>(type != null, nameof(type));
            Covenant.Requires<ArgumentNullException>(targetType != null, nameof(targetType));

            do
            {
                if (type == targetType)
                {
                    return true;
                }

                type = type.BaseType;
            }
            while (type != null);

            return false;
        }


        /// <summary>
        /// Determines whether a type inherits from another type.
        /// </summary>
        /// <typeparam name="TBase">The required base type.</typeparam>
        /// <param name="type">The type being tested.</param>
        /// <returns><c>true</c> if <paramref name="type"/> inherits <paramref name="type"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="type"/> is <c>null</c>.</exception>
        /// <remarks>
        /// <note>
        /// This method also returns <c>true</c> when <typeparamref name="TBase"/> is the
        /// same as <paramref name="type"/>.
        /// </note>
        /// </remarks>
        public static bool Inherits<TBase>(this Type type)
        {
            Covenant.Requires<ArgumentNullException>(type != null, nameof(type));

            while (type != null)
            {
                if (type == typeof(TBase))
                {
                    return true;
                }

                type = type.BaseType;
            }

            return false;
        }

        //---------------------------------------------------------------------
        // System.Reflection.Method

        /// <summary>
        /// Returns the array of types for a method's parameters.
        /// </summary>
        /// <param name="method">The method.</param>
        /// <returns>The parameter type array.</returns>
        public static Type[] GetParameterTypes(this MethodInfo method)
        {
            var methodParameters     = method.GetParameters();
            var methodParameterTypes = new Type[methodParameters.Length];

            for (int i = 0; i < methodParameters.Length; i++)
            {
                methodParameterTypes[i] = methodParameters[i].ParameterType;
            }

            return methodParameterTypes;
        }
    }
}
