//-----------------------------------------------------------------------------
// FILE:	    PropertyNames.cs
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
using System.Reflection;
using System.Text;

using Newtonsoft.Json;

using Neon.Cadence;
using Neon.Common;
using System.Diagnostics.Contracts;

namespace Neon.Cadence.Internal
{
    /// <summary>
    /// Enumerates the Cadence proxy message property names as <see cref="PropertyNameUtf8"/>
    /// values that have the UTF-8 encodings pre-computed.
    /// </summary>
    internal static class PropertyNames
    {
        //---------------------------------------------------------------------
        // Proxy message property names.

        public static PropertyNameUtf8 Activity { get; private set; }                                  = new PropertyNameUtf8("Activity");
        public static PropertyNameUtf8 ActivityContextId { get; private set; }                         = new PropertyNameUtf8("ActivityContextId");
        public static PropertyNameUtf8 ActivityId { get; private set; }                                = new PropertyNameUtf8("ActivityId");
        public static PropertyNameUtf8 ActivityTypeId { get; private set; }                            = new PropertyNameUtf8("ActivityTypeId");
        public static PropertyNameUtf8 Args { get; private set; }                                      = new PropertyNameUtf8("Args");
        public static PropertyNameUtf8 ChangeId { get; private set; }                                  = new PropertyNameUtf8("ChangeId");
        public static PropertyNameUtf8 ChildId { get; private set; }                                   = new PropertyNameUtf8("ChildId");
        public static PropertyNameUtf8 ClientTimeout { get; private set; }                             = new PropertyNameUtf8("ClientTimeout");
        public static PropertyNameUtf8 ConfigurationEmitMetrics { get; private set; }                  = new PropertyNameUtf8("ConfigurationEmitMetrics");
        public static PropertyNameUtf8 ConfigurationRetentionDays { get; private set; }                = new PropertyNameUtf8("ConfigurationRetentionDays");
        public static PropertyNameUtf8 ContextId { get; private set; }                                 = new PropertyNameUtf8("ContextId");
        public static PropertyNameUtf8 ContinueAsNew { get; private set; }                             = new PropertyNameUtf8("ContinueAsNew");
        public static PropertyNameUtf8 ContinueAsNewArgs { get; private set; }                         = new PropertyNameUtf8("ContinueAsNewArgs");
        public static PropertyNameUtf8 ContinueAsNewDomain { get; private set; }                       = new PropertyNameUtf8("ContinueAsNewDomain");
        public static PropertyNameUtf8 ContinueAsNewExecutionStartToCloseTimeout { get; private set; } = new PropertyNameUtf8("ContinueAsNewExecutionStartToCloseTimeout");
        public static PropertyNameUtf8 ContinueAsNewScheduleToCloseTimeout { get; private set; }       = new PropertyNameUtf8("ContinueAsNewScheduleToCloseTimeout");
        public static PropertyNameUtf8 ContinueAsNewScheduleToStartTimeout { get; private set; }       = new PropertyNameUtf8("ContinueAsNewScheduleToStartTimeout");
        public static PropertyNameUtf8 ContinueAsNewStartToCloseTimeout { get; private set; }          = new PropertyNameUtf8("ContinueAsNewStartToCloseTimeout");
        public static PropertyNameUtf8 ContinueAsNewTaskList { get; private set; }                     = new PropertyNameUtf8("ContinueAsNewTaskList");
        public static PropertyNameUtf8 CreateDomain { get; private set; }                              = new PropertyNameUtf8("CreateDomain");
        public static PropertyNameUtf8 Description { get; private set; }                               = new PropertyNameUtf8("Description");
        public static PropertyNameUtf8 Details { get; private set; }                                   = new PropertyNameUtf8("Details");
        public static PropertyNameUtf8 Domain { get; private set; }                                    = new PropertyNameUtf8("Domain");
        public static PropertyNameUtf8 DomainInfoName { get; private set; }                            = new PropertyNameUtf8("DomainInfoName");
        public static PropertyNameUtf8 DomainInfoOwnerEmail { get; private set; }                      = new PropertyNameUtf8("DomainInfoOwnerEmail");
        public static PropertyNameUtf8 DomainInfoStatus { get; private set; }                          = new PropertyNameUtf8("DomainInfoStatus");
        public static PropertyNameUtf8 DomainInfoDescription { get; private set; }                     = new PropertyNameUtf8("DomainInfoDescription");
        public static PropertyNameUtf8 Duration { get; private set; }                                  = new PropertyNameUtf8("Duration");
        public static PropertyNameUtf8 EmitMetrics { get; private set; }                               = new PropertyNameUtf8("EmitMetrics");
        public static PropertyNameUtf8 Endpoints { get; private set; }                                 = new PropertyNameUtf8("Endpoints");
        public static PropertyNameUtf8 Error { get; private set; }                                     = new PropertyNameUtf8("Error");
        public static PropertyNameUtf8 Execution { get; private set; }                                 = new PropertyNameUtf8("Execution");
        public static PropertyNameUtf8 ExecutionStartToCloseTimeout { get; private set; }              = new PropertyNameUtf8("ExecutionStartToCloseTimeout");
        public static PropertyNameUtf8 HasDetails { get; private set; }                                = new PropertyNameUtf8("HasDetails");
        public static PropertyNameUtf8 HasResult { get; private set; }                                 = new PropertyNameUtf8("HasResult");
        public static PropertyNameUtf8 Identity { get; private set; }                                  = new PropertyNameUtf8("Identity");
        public static PropertyNameUtf8 Info { get; private set; }                                      = new PropertyNameUtf8("Info");
        public static PropertyNameUtf8 IsCancellable { get; private set; }                             = new PropertyNameUtf8("IsCancellable");
        public static PropertyNameUtf8 IsWorkflow { get; private set; }                                = new PropertyNameUtf8("IsWorkflow");
        public static PropertyNameUtf8 LibraryAddress { get; private set; }                            = new PropertyNameUtf8("LibraryAddress");
        public static PropertyNameUtf8 LibraryPort { get; private set; }                               = new PropertyNameUtf8("LibraryPort");
        public static PropertyNameUtf8 MinSupported { get; private set; }                              = new PropertyNameUtf8("MinSupported");
        public static PropertyNameUtf8 MaxSupported { get; private set; }                              = new PropertyNameUtf8("MaxSupported");
        public static PropertyNameUtf8 MutableId { get; private set; }                                 = new PropertyNameUtf8("MutableId");
        public static PropertyNameUtf8 Name { get; private set; }                                      = new PropertyNameUtf8("Name");
        public static PropertyNameUtf8 Options { get; private set; }                                   = new PropertyNameUtf8("Options");
        public static PropertyNameUtf8 OwnerEmail { get; private set; }                                = new PropertyNameUtf8("OwnerEmail");
        public static PropertyNameUtf8 Pending { get; private set; }                                   = new PropertyNameUtf8("Pending");
        public static PropertyNameUtf8 QueryArgs { get; private set; }                                 = new PropertyNameUtf8("QueryArgs");
        public static PropertyNameUtf8 QueryName { get; private set; }                                 = new PropertyNameUtf8("QueryName");
        public static PropertyNameUtf8 Reason { get; private set; }                                    = new PropertyNameUtf8("Reason");
        public static PropertyNameUtf8 RequestId { get; private set; }                                 = new PropertyNameUtf8("RequestId");
        public static PropertyNameUtf8 Result { get; private set; }                                    = new PropertyNameUtf8("Result");
        public static PropertyNameUtf8 RetentionDays { get; private set; }                             = new PropertyNameUtf8("RetentionDays");
        public static PropertyNameUtf8 ReplayStatus { get; private set; }                              = new PropertyNameUtf8("ReplayStatus");
        public static PropertyNameUtf8 RetryAttempts { get; private set; }                             = new PropertyNameUtf8("RetryAttempts");
        public static PropertyNameUtf8 RetryDelay { get; private set; }                                = new PropertyNameUtf8("RetryDelay");
        public static PropertyNameUtf8 RunId { get; private set; }                                     = new PropertyNameUtf8("RunId");
        public static PropertyNameUtf8 SecurityToken { get; private set; }                             = new PropertyNameUtf8("SecurityToken");
        public static PropertyNameUtf8 SignalArgs { get; private set; }                                = new PropertyNameUtf8("SignalArgs");
        public static PropertyNameUtf8 SignalName { get; private set; }                                = new PropertyNameUtf8("SignalName");
        public static PropertyNameUtf8 Size { get; private set; }                                      = new PropertyNameUtf8("Size");
        public static PropertyNameUtf8 TargetRequestId { get; private set; }                           = new PropertyNameUtf8("TargetRequestId");
        public static PropertyNameUtf8 TaskList { get; private set; }                                  = new PropertyNameUtf8("TaskList");
        public static PropertyNameUtf8 TaskToken { get; private set; }                                 = new PropertyNameUtf8("TaskToken");
        public static PropertyNameUtf8 Time { get; private set; }                                      = new PropertyNameUtf8("Time");
        public static PropertyNameUtf8 Update { get; private set; }                                    = new PropertyNameUtf8("Update");
        public static PropertyNameUtf8 UpdatedInfoDescription { get; private set; }                    = new PropertyNameUtf8("UpdatedInfoDescription");
        public static PropertyNameUtf8 UpdatedInfoOwnerEmail { get; private set; }                     = new PropertyNameUtf8("UpdatedInfoOwnerEmail");
        public static PropertyNameUtf8 Version { get; private set; }                                   = new PropertyNameUtf8("Version");
        public static PropertyNameUtf8 WasCancelled { get; private set; }                              = new PropertyNameUtf8("WasCancelled");
        public static PropertyNameUtf8 WorkerId { get; private set; }                                  = new PropertyNameUtf8("WorkerId");
        public static PropertyNameUtf8 Workflow { get; private set; }                                  = new PropertyNameUtf8("Workflow");
        public static PropertyNameUtf8 WorkflowArgs { get; private set; }                              = new PropertyNameUtf8("WorkflowArgs");
        public static PropertyNameUtf8 WorkflowId { get; private set; }                                = new PropertyNameUtf8("WorkflowId");
        public static PropertyNameUtf8 WorkflowType { get; private set; }                              = new PropertyNameUtf8("WorkflowType");

        // These properties are used for unit testing:

        public static PropertyNameUtf8 TestOne { get; private set; }                                   = new PropertyNameUtf8("TestOne");
        public static PropertyNameUtf8 TestTwo { get; private set; }                                   = new PropertyNameUtf8("TestTwo");
        public static PropertyNameUtf8 TestEmpty { get; private set; }                                 = new PropertyNameUtf8("TestEmpty");
        public static PropertyNameUtf8 TestNull { get; private set; }                                  = new PropertyNameUtf8("TestNull");
        public static PropertyNameUtf8 TestComplex { get; private set; }                               = new PropertyNameUtf8("TestComplex");
        public static PropertyNameUtf8 TestPerson { get; private set; }                                = new PropertyNameUtf8("TestPerson");

        //---------------------------------------------------------------------
        // Lookup related methods

        // IMPLEMENTATION NOTE:
        // --------------------
        // We're going to implement a custom hash table here that stores all of the
        // property name instances listed above and then can lookup an instance based
        // on a Span<byte> of the UTF-8 encoded bytes for the property name.  We're
        // doing this rather than using a regular Dictionary to avoid having to
        // extract the property name bytes from serialized messages reducing the
        // number of memory allocations.

        private static List<PropertyNameUtf8>[] buckets = new List<PropertyNameUtf8>[61];

        /// <summary>
        /// Static constructor.
        /// </summary>
        static PropertyNames()
        {
            // Initialize the hash table bucket lists.

            for (int i = 0; i < buckets.Length; i++)
            {
                buckets[i] = new List<PropertyNameUtf8>();
            }

            // Reflect the static [PropertyNameUtf8] properties and add them to the hash table.

            foreach (var property in typeof(PropertyNames).GetProperties(BindingFlags.Static | BindingFlags.Public))
            {
                if (property.PropertyType != typeof(PropertyNameUtf8))
                {
                    continue;
                }

                Add((PropertyNameUtf8)property.GetValue(null));
            }
        }

        /// <summary>
        /// Adds a property name to the internal hash table.
        /// </summary>
        /// <param name="propertyName">The property name.</param>
        private static void Add(PropertyNameUtf8 propertyName)
        {
            var hashCode = propertyName.GetHashCode();

            buckets[hashCode % buckets.Length].Add(propertyName);
        }

        /// <summary>
        /// Looks up a property name from a <c>byte</c> <see cref="Span{T}"/>.
        /// </summary>
        /// <param name="byteSpan">The byte span.</param>
        /// <returns>The <see cref="PropertyNameUtf8"/>.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the requested property name does not exist.</exception>
        public static PropertyNameUtf8 Lookup(Span<byte> byteSpan)
        {
            Covenant.Requires<ArgumentNullException>(byteSpan != null);

            var hashCode = PropertyNameUtf8.ComputeHash(byteSpan);
            var list     = buckets[hashCode % buckets.Length];

            foreach (var item in list)
            {
                if (item.GetHashCode() != hashCode && item.NameUtf8.Length != byteSpan.Length)
                {
                    continue;   // Definitely not equal.
                }

                var equal = true;

                for (int i = 0; i < byteSpan.Length; i++)
                {
                    if (byteSpan[i] != item.NameUtf8[i])
                    {
                        equal = false;
                        break;
                    }
                }

                if (equal)
                {
                    return item; 
                }
            }

            // This should never happen.

            throw new KeyNotFoundException(Encoding.UTF8.GetString(byteSpan.ToArray()));
        }
    }
}
