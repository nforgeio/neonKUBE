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
        public static PropertyNameUtf8 Activity { get; private set; }                                  = new PropertyNameUtf8("Activity");
        public static PropertyNameUtf8 ActivityContextId { get; private set; }                         = new PropertyNameUtf8("ActivityContextId");
        public static PropertyNameUtf8 ActivityId { get; private set; }                                = new PropertyNameUtf8("ActivityId");
        public static PropertyNameUtf8 ActivityTypeId { get; private set; }                            = new PropertyNameUtf8("ActivityTypeId");
        public static PropertyNameUtf8 Args { get; private set; }                                      = new PropertyNameUtf8("Args");
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
        public static PropertyNameUtf8 MutableId { get; private set; }                                 = new PropertyNameUtf8("MutableId");
        public static PropertyNameUtf8 Name { get; private set; }                                      = new PropertyNameUtf8("Name");
        public static PropertyNameUtf8 Options { get; private set; }                                   = new PropertyNameUtf8("Options");
        public static PropertyNameUtf8 OwnerEmail { get; private set; }                                = new PropertyNameUtf8("OwnerEmail");
        public static PropertyNameUtf8 QueryArgs { get; private set; }                                 = new PropertyNameUtf8("QueryArgs");
        public static PropertyNameUtf8 QueryName { get; private set; }                                 = new PropertyNameUtf8("QueryName");
        public static PropertyNameUtf8 Reason { get; private set; }                                    = new PropertyNameUtf8("Reason");
        public static PropertyNameUtf8 RequestId { get; private set; }                                 = new PropertyNameUtf8("RequestId");
        public static PropertyNameUtf8 Result { get; private set; }                                    = new PropertyNameUtf8("Result");
        public static PropertyNameUtf8 RetentionDays { get; private set; }                             = new PropertyNameUtf8("RetentionDays");
        public static PropertyNameUtf8 RunId { get; private set; }                                     = new PropertyNameUtf8("RunId");
        public static PropertyNameUtf8 SignalArgs { get; private set; }                                = new PropertyNameUtf8("SignalArgs");
        public static PropertyNameUtf8 SignalName { get; private set; }                                = new PropertyNameUtf8("SignalName");
        public static PropertyNameUtf8 Size { get; private set; }                                      = new PropertyNameUtf8("Size");
        public static PropertyNameUtf8 TargetRequestId { get; private set; }                           = new PropertyNameUtf8("TargetRequestId");
        public static PropertyNameUtf8 TaskList { get; private set; }                                  = new PropertyNameUtf8("TaskList");
        public static PropertyNameUtf8 TaskToken { get; private set; }                                 = new PropertyNameUtf8("TaskToken");
        public static PropertyNameUtf8 Time { get; private set; }                                      = new PropertyNameUtf8("Time");
        public static PropertyNameUtf8 UpdatedInfoDescription { get; private set; }                    = new PropertyNameUtf8("UpdatedInfoDescription");
        public static PropertyNameUtf8 UpdatedInfoOwnerEmail { get; private set; }                     = new PropertyNameUtf8("UpdatedInfoOwnerEmail");
        public static PropertyNameUtf8 WasCancelled { get; private set; }                              = new PropertyNameUtf8("WasCancelled");
        public static PropertyNameUtf8 WorkerId { get; private set; }                                  = new PropertyNameUtf8("WorkerId");
        public static PropertyNameUtf8 Workflow { get; private set; }                                  = new PropertyNameUtf8("Workflow");
        public static PropertyNameUtf8 WorkflowArgs { get; private set; }                              = new PropertyNameUtf8("WorkflowArgs");
        public static PropertyNameUtf8 WorkflowId { get; private set; }                                = new PropertyNameUtf8("WorkflowId");
        public static PropertyNameUtf8 WorkflowType { get; private set; }                              = new PropertyNameUtf8("WorkflowType");
    }
}
