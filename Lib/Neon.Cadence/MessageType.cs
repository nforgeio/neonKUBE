//-----------------------------------------------------------------------------
// FILE:	    MessageType.cs
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
using System.Linq;
using System.Text;

using Newtonsoft.Json;
using YamlDotNet.Serialization;

using Neon.Common;

namespace Neon.Cadence
{
    /// <summary>
    /// Enumerates the possible message types.
    /// </summary>
    internal static class MessageType
    {
        /// <summary>
        /// Indicates an unspecified operation.
        /// </summary>
        public const int Unknown = 0;

        /// <summary>
        /// library --> proxy: Informs the proxy of the network endpoint it should transmit
        /// messages to the library and also includes the settings required to establish
        /// a connection with a Cadence cluster.  The proxy will establish the Cadence cluster
        /// connection and send a <see cref="ConnectReply"/> message back to the libary
        /// indicating success or failure.
        /// </summary>
        public const int ConnectRequest = 1;

        /// <summary>
        /// proxy --> library: Send in reponse to a <see cref="ConnectRequest"/> message.
        /// </summary>
        public const int ConnectReply = 2;

        /// <summary>
        /// library --> proxy: Signals the proxy that it should terminate gracefully.  The
        /// proxy should send a <see cref="TerminateReply"/> back to the library and
        /// then exit, terminating the process.
        /// </summary>
        public const int TerminateRequest = 3;

        /// <summary>
        /// proxy --> library: Send in reponse to a <see cref="ConnectRequest"/> message.
        /// </summary>
        public const int TerminateReply = 4;

        //---------------------------------------------------------------------
        // Workflow client messages
        //
        // Note that all workflow client request messages will include [WorkflowClientId] argument
        // identifying the target workflow client.

        /// <summary>
        /// library --> proxy: Creates a workflow client. 
        /// </summary>
        public const int Workflow_NewClientRequest = 100;

        /// <summary>
        /// proxy --> library: Send in reponse to a <see cref="Workflow_NewClientRequest"/> message.
        /// </summary>
        public const int Workflow_NewClientReply = 101;

        /// <summary>
        /// library --> proxy: Starts a workflow.
        /// </summary>
        public const int Workflow_StartWorkflowRequest = 102;

        /// <summary>
        /// proxy --> library: Send in reponse to a <see cref="Workflow_StartWorkflowRequest"/> message.
        /// </summary>
        public const int Workflow_StartWorkflowReply = 103;

        /// <summary>
        /// library --> proxy: Executes a workflow.
        /// </summary>
        public const int Workflow_ExecuteWorkflowRequest = 104;

        /// <summary>
        /// proxy --> library: Send in reponse to a <see cref="Workflow_ExecuteWorkflowRequest"/> message.
        /// </summary>
        public const int Workflow_ExecuteWorkflowReply = 105;

        /// <summary>
        /// library --> proxy: Signals a workflow.
        /// </summary>
        public const int Workflow_SignalWorkflowRequest = 106;

        /// <summary>
        /// proxy --> library: Send in reponse to a <see cref="Workflow_SignalWorkflowRequest"/> message.
        /// </summary>
        public const int Workflow_SignalWorkslowReply = 107;

        /// <summary>
        /// library --> proxy: Signals a workflow, starting it if necessary.
        /// </summary>
        public const int Workflow_SignalWorkflowWithStartRequest = 108;

        /// <summary>
        /// proxy --> library: Send in reponse to a <see cref="Workflow_SignalWorkflowWithStartRequest"/> message.
        /// </summary>
        public const int Workflow_SignalWorkflowWithStartReply = 109;

        /// <summary>
        /// library --> proxy: Cancels a workflow.
        /// </summary>
        public const int Workflow_CancelWorkflowRequest = 110;

        /// <summary>
        /// proxy --> library: Send in reponse to a <see cref="Workflow_CancelWorkflowRequest"/> message.
        /// </summary>
        public const int Workflow_CancelWorkflowReply = 111;

        /// <summary>
        /// library --> proxy: Terminates a workflow.
        /// </summary>
        public const int Workflow_TerminateWorkflowRequest = 112;

        /// <summary>
        /// proxy --> library: Send in reponse to a <see cref="Workflow_TerminateWorkflowRequest"/> message.
        /// </summary>
        public const int Workflow_TerminateWorkflowReply = 113;

        /// <summary>
        /// library --> proxy: Requests the a workflow's history.
        /// </summary>
        public const int Workflow_GetWorkflowHistoryRequest = 114;

        /// <summary>
        /// proxy --> library: Send in reponse to a <see cref="Workflow_GetWorkflowHistoryRequest"/> message.
        /// </summary>
        public const int Workflow_GetWorkflowHistoryReply = 115;

        /// <summary>
        /// library --> proxy: Indicates that an activity has completed.
        /// </summary>
        public const int Workflow_CompleteActivityRequest = 116;

        /// <summary>
        /// proxy --> library: Send in reponse to a <see cref="Workflow_CompleteActivityRequest"/> message.
        /// </summary>
        public const int Workflow_CompleteActivityReply = 117;

        /// <summary>
        /// library --> proxy: Indicates that the activity with a specified ID as completed has completed.
        /// </summary>
        public const int Workflow_CompleteActivityByIdRequest = 118;

        /// <summary>
        /// proxy --> library: Send in reponse to a <see cref="Workflow_CompleteActivityByIdRequest"/> message.
        /// </summary>
        public const int Workflow_CompleteActivityByIdReply = 119;

        /// <summary>
        /// library --> proxy: Records an activity heartbeat.
        /// </summary>
        public const int Workflow_RecordActivityHeartbeatRequest = 120;

        /// <summary>
        /// proxy --> library: Send in reponse to a <see cref="Workflow_RecordActivityHeartbeatRequest"/> message.
        /// </summary>
        public const int Workflow_RecordActivityHeartbeatReply = 121;

        /// <summary>
        /// library --> proxy: Records a heartbeat for an activity specified by ID.
        /// </summary>
        public const int Workflow_RecordActivityHeartbeatByIdRequest = 122;

        /// <summary>
        /// proxy --> library: Send in reponse to a <see cref="Workflow_RecordActivityHeartbeatByIdRequest"/> message.
        /// </summary>
        public const int Workflow_RecordActivityHeartbeatByIdReply = 123;

        /// <summary>
        /// library --> proxy: Requests the list of closed workflows.
        /// </summary>
        public const int Workflow_ListClosedWorkflowRequest = 124;

        /// <summary>
        /// proxy --> library: Send in reponse to a <see cref="Workflow_ListClosedWorkflowRequest"/> message.
        /// </summary>
        public const int Workflow_ListClosedWorkflowReply = 125;

        /// <summary>
        /// library --> proxy: Requests the list of open workflows.
        /// </summary>
        public const int Workflow_ListOpenWorkflowRequest = 126;

        /// <summary>
        /// proxy --> library: Send in reponse to a <see cref="Workflow_ListOpenWorkflowRequest"/> message.
        /// </summary>
        public const int Workflow_ListOpenWorkflowReply = 127;

        /// <summary>
        /// library --> proxy: Queries a workflow's last execution.
        /// </summary>
        public const int Workflow_QueryWorkflowRequest = 128;

        /// <summary>
        /// proxy --> library: Send in reponse to a <see cref="Workflow_QueryWorkflowRequest"/> message.
        /// </summary>
        public const int Workflow_QueryWorkflowReply = 129;

        /// <summary>
        /// library --> proxy: Returns information about a worflow execution.
        /// </summary>
        public const int Workflow_DescribeWorkflowExecutionRequest = 130;

        /// <summary>
        /// proxy --> library: Send in reponse to a <see cref="Workflow_DescribeWorkflowExecutionRequest"/> message.
        /// </summary>
        public const int Workflow_DescribeWorkflowExecutionReply = 131;

        /// <summary>
        /// <b>RESERVED:</b> This is not currently implemented.
        /// </summary>
        [Obsolete("RESERVED but not implemented.")]
        public const int Workflow_DescribeTaskListRequest = 132;

        /// <summary>
        /// <b>RESERVED:</b> This is not currently implemented.
        /// </summary>
        [Obsolete("RESERVED but not implemented.")]
        public const int Workflow_DescribeTaskListReply = 133;

        //---------------------------------------------------------------------
        // Domain messages
        //
        // Note that all domain client request messages will include DomainClientId] argument
        // identifying the target domain client.

        /// <summary>
        /// library --> proxy: Creates a domain client.
        /// </summary>
        public const int Domain_NewDomainClientRequest = 200;

        /// <summary>
        /// proxy --> library: Send in reponse to a <see cref="Domain_NewDomainClientRequest"/> message.
        /// </summary>
        public const int Domain_NewDomainClientReply = 201;

        /// <summary>
        /// library --> proxy: Registers a Cadence domain.
        /// </summary>
        public const int Domain_RegisterRequest = 202;

        /// <summary>
        /// proxy --> library: Send in reponse to a <see cref="Domain_RegisterRequest"/> message.
        /// </summary>
        public const int Domain_RegisterReply = 203;

        /// <summary>
        /// library --> proxy: Describes a Cadence domain.
        /// </summary>
        public const int Domain_DescribeRequest = 204;

        /// <summary>
        /// proxy --> library: Send in reponse to a <see cref="Domain_DescribeRequest"/> message.
        /// </summary>
        public const int Domain_DescribeReply = 205;

        /// <summary>
        /// library --> proxy: Updates a Cadence domain.
        /// </summary>
        public const int Domain_UpdateRequest = 206;

        /// <summary>
        /// proxy --> library: Send in reponse to a <see cref="Domain_UpdateRequest"/> message.
        /// </summary>
        public const int Domain_UpdateReply = 207;
    }
}
