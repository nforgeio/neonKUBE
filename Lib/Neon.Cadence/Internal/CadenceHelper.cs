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

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;

namespace Neon.Cadence.Internal
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
        public const string WorkflowTypeMethodSeparator = "::";

        /// <summary>
        /// The optional separator string used to separate the base activity type
        /// name from the optional activity method name.  This string may not be
        /// embedded in a normal activity type name.
        /// </summary>
        public const string ActivityTypeMethodSeparator = "::";

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
        /// <exception cref="WorkflowTypeException">Thrown if the name passed is not valid.</exception>
        public static void ValidateWorkflowTypeName(string name)
        {
            if (name != null && name.Contains(CadenceHelper.WorkflowTypeMethodSeparator))
            {
                throw new WorkflowTypeException($"Workflow type names cannot include: \"{CadenceHelper.WorkflowTypeMethodSeparator}\".");
            }
        }

        /// <summary>
        /// Returns the Cadence workflow type name to be used for a workflow interface or
        /// implementation class.
        /// </summary>
        /// <param name="workflowType">The workflow interface or implementation type.</param>
        /// <param name="workflowAttribute">Optionally specifies the <see cref="WorkflowAttribute"/>.</param>
        /// <returns>The type name.</returns>
        /// <remarks>
        /// <para>
        /// If <paramref name="workflowAttribute"/> is passed and <see cref="WorkflowAttribute.TypeName"/>
        /// is not <c>null</c> or empty, then the name specified in the attribute is returned.
        /// </para>
        /// <para>
        /// Otherwise, for workflow implementations we'll return the fully qualified class name.
        /// For workflow interfaces, we'll return the fully qualified interface name but 
        /// <b>we'll strip any leading "I"</b> from the interface name as a huristic in an
        /// attempt to ensure that the workflow type name for the interface matches that for
        /// the workflow implementation.
        /// </para>
        /// <para>
        /// For situations where this huristic won't work, you'll need to specify the same
        /// workflow type name in the <see cref="WorkflowAttribute"/> for both the workflow
        /// interface and implementation.
        /// </para>
        /// </remarks>
        public static string GetWorkflowTypeName(Type workflowType, WorkflowAttribute workflowAttribute = null)
        {
            Covenant.Requires<ArgumentNullException>(workflowType != null);

            if (workflowAttribute != null && !string.IsNullOrEmpty(workflowAttribute.TypeName))
            {
                return workflowAttribute.TypeName;
            }

            var fullName = workflowType.FullName;

            if (workflowType.IsInterface)
            {
                CadenceHelper.ValidateWorkflowInterface(workflowType);

                var name = workflowType.Name;

                if (name.StartsWith("I"))
                {
                    fullName  = fullName.Substring(0, fullName.Length - name.Length);
                    fullName += name.Substring(1);
                }
            }
            else
            {
                CadenceHelper.ValidateWorkflowImplementation(workflowType);
            }

            // .NET uses "+" for nested type names.  We'll to convert these to "."
            // because "+" is a bit weird.

            return fullName.Replace('+', '.');
        }

        /// <summary>
        /// Returns the Cadence activity type name to be used for an acvtivity interface or
        /// implementation class.
        /// </summary>
        /// <param name="activityType">The activity interface or implementation type.</param>
        /// <param name="activityAttribute">Optionally specifies the <see cref="ActivityAttribute"/>.</param>
        /// <returns>The type name.</returns>
        /// <remarks>
        /// <para>
        /// If <paramref name="activityAttribute"/> is passed and <see cref="ActivityAttribute.TypeName"/>
        /// is not <c>null</c> or empty, then the name specified in the attribute is returned.
        /// </para>
        /// <para>
        /// Otherwise, for activity implementations we'll return the fully qualified class name.
        /// For activity interfaces, we'll return the fully qualified interface name but 
        /// <b>we'll strip any leading "I"</b> from the interface name as a huristic in an
        /// attempt to ensure that the activity type name for the interface matches that for
        /// the activity implementation.
        /// </para>
        /// <para>
        /// For situations where this huristic won't work, you'll need to specify the same
        /// activity type name in the <see cref="ActivityAttribute"/> for both the activity
        /// interface and implementation.
        /// </para>
        /// </remarks>
        public static string GetActivityTypeName(Type activityType, ActivityAttribute activityAttribute = null)
        {
            Covenant.Requires<ArgumentNullException>(activityType != null);

            if (activityAttribute != null && !string.IsNullOrEmpty(activityAttribute.TypeName))
            {
                return activityAttribute.TypeName;
            }

            var fullName = activityType.FullName;

            if (activityType.IsInterface)
            {
                CadenceHelper.ValidateActivityInterface(activityType);

                var name = activityType.Name;

                if (name.StartsWith("I"))
                {
                    fullName  = fullName.Substring(0, fullName.Length - name.Length);
                    fullName += name.Substring(1);
                }
            }
            else
            {
                CadenceHelper.ValidateActivityImplementation(activityType);

                fullName = activityType.FullName;
            }

            // .NET uses "+" for nested type names.  We'll to convert these to "."
            // because "+" is a bit weird.

            return fullName.Replace('+', '.');
        }

        /// <summary>
        /// Ensures that the type passed is a valid workflow interface.
        /// </summary>
        /// <param name="workflowInterface">The type being tested.</param>
        /// <exception cref="ActivityTypeException">Thrown when the interface is not valid.</exception>
        public static void ValidateWorkflowInterface(Type workflowInterface)
        {
            Covenant.Requires<ArgumentNullException>(workflowInterface != null);

            if (!workflowInterface.IsInterface)
            {
                throw new WorkflowTypeException($"[{workflowInterface.FullName}] is not an interface.");
            }

            if (workflowInterface.IsGenericType)
            {
                throw new WorkflowTypeException($"[{workflowInterface.FullName}] has generic type parameters.  Workflow interfaces may not be generic.");
            }

            if (workflowInterface == typeof(IWorkflowBase))
            {
                throw new WorkflowTypeException($"The base [{nameof(IWorkflowBase)}] interface cannot be used to define a workflow.");
            }

            if (!workflowInterface.IsPublic && !workflowInterface.IsNestedPublic)
            {
                throw new WorkflowTypeException($"Workflow interface [{workflowInterface.FullName}] is not public.");
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
                    throw new WorkflowTypeException($"Multiple workflow methods are tagged by [WorkflowMethod(Name = \"{name}\")].");
                }

                workflowNames.Add(name);
            }
        }

        /// <summary>
        /// Ensures that the type passed is a valid workflow implementation.
        /// </summary>
        /// <param name="workflowType">The type being tested.</param>
        /// <exception cref="WorkflowTypeException">Thrown when the interface is not valid.</exception>
        public static void ValidateWorkflowImplementation(Type workflowType)
        {
            Covenant.Requires<ArgumentNullException>(workflowType != null);

            if (workflowType.IsInterface)
            {
                throw new WorkflowTypeException($"[{workflowType.FullName}] workflow implementation cannot be an interface.");
            }

            if (workflowType.IsValueType)
            {
                throw new WorkflowTypeException($"[{workflowType.FullName}] workflow implementation cannot be a struct.");
            }

            if (workflowType.IsGenericType)
            {
                throw new WorkflowTypeException($"[{workflowType.FullName}] has generic type parameters.  Workflow implementations may not be generic.");
            }

            if (!workflowType.Implements<IWorkflowBase>())
            {
                throw new WorkflowTypeException($"[{workflowType.FullName}] does not derive from [{typeof(IWorkflowBase).FullName}].");
            }

            if (workflowType == typeof(WorkflowBase))
            {
                throw new WorkflowTypeException($"The base [{nameof(WorkflowBase)}] class cannot be a workflow implementation.");
            }

            var workflowNames = new HashSet<string>();

            foreach (var method in workflowType.GetMethods())
            {
                var workflowMethodAttribute = method.GetCustomAttribute<WorkflowMethodAttribute>();

                if (workflowMethodAttribute == null)
                {
                    continue;
                }

                var name = workflowMethodAttribute.Name ?? string.Empty;

                if (workflowNames.Contains(name))
                {
                    throw new WorkflowTypeException($"Multiple [{workflowType.FullName}] workflow methods are tagged by [WorkflowMethod(Name = \"{name}\")].");
                }

                workflowNames.Add(name);
            }
        }

        /// <summary>
        /// Ensures that an activity type name is valid.
        /// </summary>
        /// <param name="name">The activity type name being checked.</param>
        /// <exception cref="ActivityTypeException">Thrown if the name passed is not valid.</exception>
        public static void ValidateActivityTypeName(string name)
        {
            if (name != null && name.Contains(CadenceHelper.ActivityTypeMethodSeparator))
            {
                throw new ActivityTypeException($"Activity type names cannot include: \"{CadenceHelper.ActivityTypeMethodSeparator}\".");
            }
        }

        /// <summary>
        /// Ensures that the type passed is a valid activity interface.
        /// </summary>
        /// <param name="activityInterface">The type being tested.</param>
        /// <exception cref="ActivityTypeException">Thrown when the interface is not valid.</exception>
        public static void ValidateActivityInterface(Type activityInterface)
        {
            Covenant.Requires<ArgumentNullException>(activityInterface != null);

            if (!activityInterface.IsInterface)
            {
                throw new ActivityTypeException($"[{activityInterface.FullName}] is not an interface.");
            }

            if (activityInterface.IsGenericType)
            {
                throw new ActivityTypeException($"[{activityInterface.FullName}] has generic type parameters.  Activity interfaces may not be generic.");
            }

            if (!activityInterface.Implements<IActivityBase>())
            {
                throw new ActivityTypeException($"[{activityInterface.FullName}] does not derive from [{typeof(IActivityBase).FullName}].");
            }

            if (activityInterface == typeof(IActivityBase))
            {
                throw new ActivityTypeException($"[{nameof(IActivityBase)}] cannot be used to define an activity.");
            }

            if (!activityInterface.IsPublic && !activityInterface.IsNestedPublic)
            {
                throw new ActivityTypeException($"Activity interface [{activityInterface.FullName}] is not public.");
            }

            var activityNames = new HashSet<string>();

            foreach (var method in activityInterface.GetMethods())
            {
                var activityMethodAttribute = method.GetCustomAttribute<ActivityMethodAttribute>();

                if (activityMethodAttribute == null)
                {
                    continue;
                }

                var name = activityMethodAttribute.Name ?? string.Empty;

                if (activityNames.Contains(name))
                {
                    throw new ActivityTypeException($"Multiple [{activityInterface.FullName}] activity methods are tagged by [ActivityMethod(Name = \"{name}\")].");
                }

                activityNames.Add(name);
            }
        }

        /// <summary>
        /// Ensures that the type passed is a valid activity implementation.
        /// </summary>
        /// <param name="activityType">The type being tested.</param>
        /// <exception cref="ActivityTypeException">Thrown when the interface is not valid.</exception>
        public static void ValidateActivityImplementation(Type activityType)
        {
            Covenant.Requires<ArgumentNullException>(activityType != null);

            if (activityType.IsInterface)
            {
                throw new ActivityTypeException($"[{activityType.FullName}] implementation cannot be an interface.");
            }

            if (activityType.IsGenericType)
            {
                throw new ActivityTypeException($"[{activityType.FullName}] has generic type parameters.  Activity implementations may not be generic.");
            }

            if (!activityType.Implements<IActivityBase>())
            {
                throw new ActivityTypeException($"[{activityType.FullName}] does not derive from [{typeof(IActivityBase).FullName}].");
            }

            if (activityType == typeof(IActivityBase))
            {
                throw new ActivityTypeException($"[{nameof(IActivityBase)}] cannot be used to define an activity.");
            }

            var activityNames = new HashSet<string>();

            foreach (var method in activityType.GetMethods())
            {
                var activityMethodAttribute = method.GetCustomAttribute<ActivityMethodAttribute>();

                if (activityMethodAttribute == null)
                {
                    continue;
                }

                var name = activityMethodAttribute.Name ?? string.Empty;

                if (activityNames.Contains(name))
                {
                    throw new ActivityTypeException($"Multiple [{activityType.FullName}] activity methods are tagged by [ActivityMethod(Name = \"{name}\")].");
                }

                activityNames.Add(name);
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
