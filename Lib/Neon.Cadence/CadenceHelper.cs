//-----------------------------------------------------------------------------
// FILE:	    CadenceHelper.cs
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
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Reflection;
using Microsoft.CodeAnalysis;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;

namespace Neon.Cadence
{
    /// <summary>
    /// Cadence helper methods and constants.
    /// </summary>
    internal static class CadenceHelper
    {
        /// <summary>
        /// The optional separator string used to separate the base workflow type
        /// name from the optional workflow method name.  This string may not be
        /// embedded in a normal workflow type name.
        /// </summary>
        public const string WorkflowMethodSeparator = "::";

        /// <summary>
        /// Number of nanoseconds per second (spoiler alert: it's 1 billion).
        /// </summary>
        public const long NanosecondsPerSecond = 1000000000L;

        /// <summary>
        /// Returns the maximum timespan supported by Cadence.
        /// </summary>
        public static TimeSpan MaxTimespan { get; private set; } = TimeSpan.FromTicks(long.MaxValue / 100);

        /// <summary>
        /// Returns the minimum timespan supported by Cadence.
        /// </summary>
        public static TimeSpan MinTimespan { get; private set; } = TimeSpan.FromTicks(long.MinValue / 100);

        /// <summary>
        /// Ensures that a workflow type name is valid.
        /// </summary>
        /// <param name="name">The workflow type name being checked.</param>
        /// <exception cref="ArgumentException">Thrown if the name passed is not valid.</exception>
        public static void ValidateWorkflowTypeName(string name)
        {
            if (name != null && name.Contains(CadenceHelper.WorkflowMethodSeparator))
            {
                throw new ArgumentException($"Workflow type names cannot include \"{CadenceHelper.WorkflowMethodSeparator}\".");
            }
        }

        /// <summary>
        /// Ensures that the type passed is a valid workflow interface.
        /// </summary>
        /// <param name="workflowInterface">The type being tested.</param>
        /// <exception cref="ArgumentException">Thrown when the interface is not valid.</exception>
        public static void ValidateWorkflowInterface(Type workflowInterface)
        {
            Covenant.Requires<ArgumentNullException>(workflowInterface != null);

            if (!workflowInterface.IsInterface)
            {
                throw new ArgumentException($"[{workflowInterface.FullName}] is not an interface.");
            }

            if (!workflowInterface.IsGenericType)
            {
                throw new ArgumentException($"[{workflowInterface.FullName}] is a generic type.  Workflow interfaces may not be generic.");
            }

            if (!workflowInterface.Implements<IWorkflowBase>())
            {
                throw new ArgumentException($"[{workflowInterface.FullName}] does not derive from [{typeof(IWorkflowBase).FullName}].");
            }

            var workflowNames = new HashSet<string>();

            foreach (var method in workflowInterface.GetMethods())
            {
                var workflowMethodAttribute = method.GetCustomAttribute<WorkflowMethodAttribute>();

                if (workflowMethodAttribute == null)
                {
                    continue;
                }

                var name = workflowMethodAttribute.Name ?? string.Empty;

                if (workflowNames.Contains(name))
                {
                    throw new ArgumentException($"Multiple workflow methods are tagged by [WorkflowMethod(name: \"{name}\")].");
                }

                workflowNames.Add(name);
            }
        }

        /// <summary>
        /// Ensures that the type passed is a valid activity interface.
        /// </summary>
        /// <param name="activityInterface">The type being tested.</param>
        /// <exception cref="ArgumentException">Thrown when the interface is not valid.</exception>
        public static void ValidateActivityInterface(Type activityInterface)
        {
            Covenant.Requires<ArgumentNullException>(activityInterface != null);

            if (!activityInterface.IsInterface)
            {
                throw new ArgumentException($"[{activityInterface.FullName}] is not an interface.");
            }

            if (!activityInterface.IsGenericType)
            {
                throw new ArgumentException($"[{activityInterface.FullName}] is a generic type.  Activity interfaces may not be generic.");
            }

            if (!activityInterface.Implements<IActivityBase>())
            {
                throw new ArgumentException($"[{activityInterface.FullName}] does not derive from [{typeof(IActivityBase).FullName}].");
            }
        }

        /// <summary>
        /// Ensures that the timespan passed doesn't exceed the minimum or maximum
        /// supported by Cadence/GOLANG.
        /// </summary>
        /// <param name="timespan">The input.</param>
        /// <returns>The adjusted output.</returns>
        public static TimeSpan Normalize(TimeSpan timespan)
        {
            if (timespan > MaxTimespan)
            {
                return MaxTimespan;
            }
            else if (timespan < MinTimespan)
            {
                return MinTimespan;
            }
            else
            {
                return timespan;
            }
        }

        /// <summary>
        /// Converts a .NET <see cref="TimeSpan"/> into a Cadence/GOLANG duration
        /// (aka a <c>long</c> specifying the interval in nanoseconds.
        /// </summary>
        /// <param name="timespan">The input .NET timespan.</param>
        /// <returns>The duration in nanoseconds.</returns>
        public static long ToCadence(TimeSpan timespan)
        {
            timespan = Normalize(timespan);

            return timespan.Ticks * 100;
        }

        /// <summary>
        /// Parses a Cadence timestamp string and converts it to a UTC
        /// <see cref="DateTime"/>.
        /// </summary>
        /// <param name="timestamp">The timestamp string.</param>
        /// <returns>The parsed <see cref="DateTime"/>.</returns>
        public static DateTime ParseCadenceTimestamp(string timestamp)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(timestamp));

            var dateTimeOffset = DateTimeOffset.Parse(timestamp, CultureInfo.InvariantCulture);

            return new DateTime(dateTimeOffset.ToUniversalTime().Ticks, DateTimeKind.Utc);
        }

        /// <summary>
        /// Returns the name we'll use for a type when generating type references.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>The type name.</returns>
        private static string GetTypeName(Type type)
        {
            // Convert common types into their C# equivents:

            var typeName = type.FullName;

            switch (typeName)
            {
                case "System.Byte":     return "byte";
                case "System.SByte":    return "sbyte";
                case "System.Int16":    return "short";
                case "System.UInt16":   return "ushort";
                case "System.Int32":    return "int";
                case "System.UInt32":   return "uint";
                case "System.Int64":    return "long";
                case "System.UInt64":   return "ulong";
                case "System.Float":    return "float";
                case "System.Double":   return "double";
                case "System.String":   return "string";
                case "System.Boolean":  return "bool";
                case "System.Decimal":  return "decimal";
            }

            if (type.IsGenericType)
            {
                // Strip the backtick and any text after it.

                var tickPos = typeName.IndexOf('`');

                if (tickPos != -1)
                {
                    typeName = typeName.Substring(0, tickPos);
                }
            }

            // We're going to use the global namespace to avoid namespace conflicts.

            return $"global::{typeName}";
        }

        /// <summary>
        /// Resolves the type passed into a nice string taking generic types 
        /// and arrays into account.  This is used when generating workflow
        /// and activity stubs.
        /// </summary>
        /// <param name="type">The referenced type.</param>
        /// <returns>The type reference as a string or <c>null</c> if the type is not valid.</returns>
        public static string TypeToCSharp(Type type)
        {
            if (type == typeof(void))
            {
                return "void";
            }

            if (type.IsPrimitive || (!type.IsArray && !type.IsGenericType))
            {
                return GetTypeName(type);
            }

            if (type.IsArray)
            {
                // We need to handle jagged arrays where the element type 
                // is also an array.  We'll accomplish this by walking down
                // the element types until we get to a non-array element type,
                // counting how many subarrays there were.

                var arrayDepth  = 0;
                var elementType = type.GetElementType();

                while (elementType.IsArray)
                {
                    arrayDepth++;
                    elementType = elementType.GetElementType();
                }

                var arrayRef = TypeToCSharp(elementType);

                for (int i = 0; i <= arrayDepth; i++)
                {
                    arrayRef += "[]";
                }

                return arrayRef;
            }
            else if (type.IsGenericType)
            {
                var genericRef    = GetTypeName(type);
                var genericParams = string.Empty;

                foreach (var genericParamType in type.GetGenericArguments())
                {
                    if (genericParams.Length > 0)
                    {
                        genericParams += ", ";
                    }

                    genericParams += TypeToCSharp(genericParamType);
                }

                return $"{genericRef}<{genericParams}>";
            }

            Covenant.Assert(false); // We should never get here.            
            return null;
        }
    }
}
