//-----------------------------------------------------------------------------
// FILE:	    ExceptionResult.cs
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
using System.Diagnostics.Contracts;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

using Newtonsoft.Json;

using Neon.Common;

namespace Neon.Common
{
    /// <summary>
    /// Used to marshal a possible exception from a remote process to the local caller.
    /// Use this type for remote methods that return <c>void</c> or the derived
    /// <see cref="ExceptionResult{TResult}"/> type for remote methods that return
    /// a result.
    /// </summary>
    /// <remarks>
    /// <note>
    /// This is typically be used internally by frameworks like <b>Neon.Cadence</b> to
    /// bring exception handling functionality when calling remote processes (like
    /// workflows and activities in that case).
    /// </note>
    /// <para>
    /// This works by setting the <see cref="ExceptionType"/> property to the fully qualified
    /// name of the exception type and <see cref="ExceptionMessage"/> to the exception message
    /// (which may be <c>null</c>) in the remote process and then serializing the instance 
    /// to JSON or some other format to be transmitted back to the caller.
    /// </para>
    /// <para>
    /// The local caller will then call <see cref="ThrowOnError"/> which will attempt to rethrow
    /// the exception with the same <see cref="ExceptionType"/>.  This is accomplished by reflecting
    /// all assemblies currently loaded in the <see cref="AppDomain"/> looking for exception
    /// types.  Exception types that have a default constructor or constructors with a string
    /// message parameter or a message and inner exception message parameters will be 
    /// cached internally in a dictionary that maps the type name to the exception type.
    /// </para>
    /// <para>
    /// If <see cref="ThrowOnError"/> can locate a local exception type that has a conforming
    /// constructor, then that exception type will be thrown with the <see cref="ExceptionMessage"/>.
    /// A <see cref="CatchAllException"/> will be thrown instead, if a conforming local
    /// exception doesn't exist.
    /// </para>
    /// </remarks>
    public class ExceptionResult
    {
        //---------------------------------------------------------------------
        // Static members

        private struct ExceptionThrower
        {
            /// <summary>
            /// Set to the local exception's default constructor (if any).
            /// </summary>
            public ConstructorInfo DefaultConstructor;

            /// <summary>
            /// Set to the local exception's constructor that accepts a single string message argument (if any).
            /// </summary>
            public ConstructorInfo MessageConstructor;

            /// <summary>
            /// Set to the local exception's constructor that accepts tring message and
            /// inner exception arguments (if any).
            /// </summary>
            public ConstructorInfo MessageInnerConstructor;

            /// <summary>
            /// Throws a local exception type, including the message passed.
            /// </summary>
            /// <param name="message"></param>
            public void Throw(string message)
            {
                if (MessageConstructor != null)
                {
                    throw (Exception)MessageConstructor.Invoke(new object[] { message });
                }
                else if (MessageInnerConstructor != null)
                {
                    throw (Exception)MessageInnerConstructor.Invoke(new object[] { message, null });
                }
                else if (DefaultConstructor != null)
                {
                    throw (Exception)DefaultConstructor.Invoke(Array.Empty<object>());
                }

                Covenant.Assert(false);
            }
        }

        private static Dictionary<string, ExceptionThrower> typeNameToThrower;

        /// <summary>
        /// Static constructor.
        /// </summary>
        static ExceptionResult()
        {
            // Reflect all loaded assemblies, looking for exception types that have at
            // least one compliant constructor and initialize a dictionary that maps 
            // the fully qualified type name of the exceptions found to an [ExceptionThrower] 
            // that can rethrow an error for the local reflected type.

            typeNameToThrower = new Dictionary<string, ExceptionThrower>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var exceptionType in assembly.GetTypes().Where(t => t.Inherits<Exception>()))
                    {
                        var defaultConstructor      = exceptionType.GetConstructor(Type.EmptyTypes);
                        var messageConstructor      = exceptionType.GetConstructor(new Type[] { typeof(string) });
                        var messageInnerConstructor = exceptionType.GetConstructor(new Type[] { typeof(string), typeof(Exception) });

                        if (defaultConstructor != null || messageConstructor != null || messageInnerConstructor != null)
                        {
                            var thrower = new ExceptionThrower()
                            {
                                DefaultConstructor = defaultConstructor,
                                MessageConstructor = messageConstructor,
                                MessageInnerConstructor = messageInnerConstructor
                            };

                            typeNameToThrower.Add(exceptionType.FullName, thrower);
                        }
                    }
                }
                catch (ReflectionTypeLoadException)
                {
                    // We can see this for special assemblies.  We're going to ignore
                    // these assemblies.
                    //
                    //      https://github.com/microsoft/vstest/issues/1098      
                }
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Default constructor.
        /// </summary>
        public ExceptionResult()
        {
        }

        /// <summary>
        /// Constructs an instance from an exception.
        /// </summary>
        /// <param name="e">The source exception.</param>
        public ExceptionResult(Exception e)
        {
            if (e != null)
            {
                ExceptionType    = e.GetType().FullName;
                ExceptionMessage = e.Message;
            }
        }

        /// <summary>
        /// The fully qualified name of the exception type to be rethrown or <c>null</c>
        /// when there was no error.
        /// </summary>
        [JsonProperty(PropertyName = "ExceptionType", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string ExceptionType { get; set; }

        /// <summary>
        /// Optionally specifies the exception message when <see cref="ExceptionType"/> is not <c>null</c>.
        /// </summary>
        [JsonProperty(PropertyName = "ExceptionMessage", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string ExceptionMessage { get; set; }

        /// <summary>
        /// This method does nothing in <see cref="ExceptionType"/> is <c>null</c> or empty, otherwise
        /// it attempts to throw the local exception type with the same name as <see cref="ExceptionType"/>
        /// and if that's not possible, it'll throw a <see cref="CatchAllException"/> including
        /// the type name for the exception and message.
        /// </summary>
        public void ThrowOnError()
        {
            if (!string.IsNullOrEmpty(ExceptionType))
            {
                lock (typeNameToThrower)
                {
                    if (typeNameToThrower.TryGetValue(ExceptionType, out var thrower))
                    {
                        thrower.Throw(ExceptionMessage);
                    }
                    else
                    {
                        throw new CatchAllException(ExceptionType, ExceptionMessage);
                    }
                }
            }
        }
    }
}
