//-----------------------------------------------------------------------------
// FILE:	    MessageTypes.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License"),
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
    internal enum MessageTypes
    {
        /// <summary>
        /// Indicates a message with an unspecified type.  This normally indicates an error.
        /// </summary>
        Unspecified = 0,

        //---------------------------------------------------------------------
        // Global messages

        /// <summary>
        /// library --> proxy: Requests that the proxy establish a connection to a Cadence
        /// cluster.  This maps to a <c>NewClient()</c> in the proxy.
        /// </summary>
        ConnectRequest = 1,

        /// <summary>
        /// proxy --> library: Sent in response to a <see cref="ConnectRequest"/> message.
        /// </summary>
        ConnectReply = 2,

        /// <summary>
        /// library --> proxy: Signals the proxy that it should terminate gracefully.  The
        /// proxy should send a <see cref="TerminateReply"/> back to the library and
        /// then exit, terminating the process.
        /// </summary>
        TerminateRequest = 3,

        /// <summary>
        /// proxy --> library: Sent in response to a <see cref="TerminateRequest"/> message.
        /// </summary>
        TerminateReply = 4,

        /// <summary>
        /// library --> proxy: Requests that the proxy register a Cadence domain.
        /// </summary>
        DomainRegisterRequest = 5,

        /// <summary>
        /// proxy --> library: Sent in response to a <see cref="DomainRegisterRequest"/> message.
        /// </summary>
        DomainRegisterReply = 6,

        /// <summary>
        /// library --> proxy: Requests that the proxy return the details for a Cadence domain.
        /// </summary>
        DomainDescribeRequest = 7,

        /// <summary>
        /// proxy --> library: Sent in response to a <see cref="DomainDescribeRequest"/> message.
        /// </summary>
        DomainDescribeReply = 8,

        /// <summary>
        /// library --> proxy: Requests that the proxy update a Cadence domain.
        /// </summary>
        DomainUpdateRequest = 9,

        /// <summary>
        /// proxy --> library: Sent in response to a <see cref="DomainUpdateRequest"/> message.
        /// </summary>
        DomainUpdateReply = 10,

        //---------------------------------------------------------------------
        // Workflow messages
        //
        // Note that all workflow client request messages will include [WorkflowClientId] argument
        // identifying the target workflow client.

        /// <summary>
        /// library --> proxy: Registers a workflow handler.
        /// </summary>
        Workflow_RegisterRequest = 100,

        /// <summary>
        /// proxy --> library: Sent in response to a <see cref="Workflow_RegisterRequest"/> message.
        /// </summary>
        Workflow_RegisterflowReply = 101,

        /// <summary>
        /// library --> proxy: Starts a workflow.
        /// </summary>
        Workflow_StartWorkflowRequest = 102,

        /// <summary>
        /// proxy --> library: Sent in response to a <see cref="Workflow_StartWorkflowRequest"/> message.
        /// </summary>
        Workflow_StartWorkflowReply = 103,

        /// <summary>
        /// library --> proxy: Executes a workflow.
        /// </summary>
        Workflow_ExecuteWorkflowRequest = 104,

        /// <summary>
        /// proxy --> library: Sent in response to a <see cref="Workflow_ExecuteWorkflowRequest"/> message.
        /// </summary>
        Workflow_ExecuteWorkflowReply = 105,

        /// <summary>
        /// library --> proxy: Signals a workflow.
        /// </summary>
        Workflow_SignalWorkflowRequest = 106,

        /// <summary>
        /// proxy --> library: Sent in response to a <see cref="Workflow_SignalWorkflowRequest"/> message.
        /// </summary>
        Workflow_SignalWorkslowReply = 107,

        /// <summary>
        /// library --> proxy: Signals a workflow, starting it if necessary.
        /// </summary>
        Workflow_SignalWorkflowWithStartRequest = 108,

        /// <summary>
        /// proxy --> library: Sent in response to a <see cref="Workflow_SignalWorkflowWithStartRequest"/> message.
        /// </summary>
        Workflow_SignalWorkflowWithStartReply = 109,

        /// <summary>
        /// library --> proxy: Cancels a workflow.
        /// </summary>
        Workflow_CancelWorkflowRequest = 110,

        /// <summary>
        /// proxy --> library: Sent in response to a <see cref="Workflow_CancelWorkflowRequest"/> message.
        /// </summary>
        Workflow_CancelWorkflowReply = 111,

        /// <summary>
        /// library --> proxy: Terminates a workflow.
        /// </summary>
        Workflow_TerminateWorkflowRequest = 112,

        /// <summary>
        /// proxy --> library: Sent in response to a <see cref="Workflow_TerminateWorkflowRequest"/> message.
        /// </summary>
        Workflow_TerminateWorkflowReply = 113,

        /// <summary>
        /// library --> proxy: Requests the a workflow's history.
        /// </summary>
        Workflow_GetWorkflowHistoryRequest = 114,

        /// <summary>
        /// proxy --> library: Sent in response to a <see cref="Workflow_GetWorkflowHistoryRequest"/> message.
        /// </summary>
        Workflow_GetWorkflowHistoryReply = 115,

        /// <summary>
        /// library --> proxy: Indicates that an activity has completed.
        /// </summary>
        Workflow_CompleteActivityRequest = 116,

        /// <summary>
        /// proxy --> library: Sent in response to a <see cref="Workflow_CompleteActivityRequest"/> message.
        /// </summary>
        Workflow_CompleteActivityReply = 117,

        /// <summary>
        /// library --> proxy: Indicates that the activity with a specified ID as completed has completed.
        /// </summary>
        Workflow_CompleteActivityByIdRequest = 118,

        /// <summary>
        /// proxy --> library: Sent in response to a <see cref="Workflow_CompleteActivityByIdRequest"/> message.
        /// </summary>
        Workflow_CompleteActivityByIdReply = 119,

        /// <summary>
        /// library --> proxy: Records an activity heartbeat.
        /// </summary>
        Workflow_RecordActivityHeartbeatRequest = 120,

        /// <summary>
        /// proxy --> library: Sent in response to a <see cref="Workflow_RecordActivityHeartbeatRequest"/> message.
        /// </summary>
        Workflow_RecordActivityHeartbeatReply = 121,

        /// <summary>
        /// library --> proxy: Records a heartbeat for an activity specified by ID.
        /// </summary>
        Workflow_RecordActivityHeartbeatByIdRequest = 122,

        /// <summary>
        /// proxy --> library: Sent in response to a <see cref="Workflow_RecordActivityHeartbeatByIdRequest"/> message.
        /// </summary>
        Workflow_RecordActivityHeartbeatByIdReply = 123,

        /// <summary>
        /// library --> proxy: Requests the list of closed workflows.
        /// </summary>
        Workflow_ListClosedWorkflowRequest = 124,

        /// <summary>
        /// proxy --> library: Sent in response to a <see cref="Workflow_ListClosedWorkflowRequest"/> message.
        /// </summary>
        Workflow_ListClosedWorkflowReply = 125,

        /// <summary>
        /// library --> proxy: Requests the list of open workflows.
        /// </summary>
        Workflow_ListOpenWorkflowRequest = 126,

        /// <summary>
        /// proxy --> library: Sent in response to a <see cref="Workflow_ListOpenWorkflowRequest"/> message.
        /// </summary>
        Workflow_ListOpenWorkflowReply = 127,

        /// <summary>
        /// library --> proxy: Queries a workflow's last execution.
        /// </summary>
        Workflow_QueryWorkflowRequest = 128,

        /// <summary>
        /// proxy --> library: Sent in response to a <see cref="Workflow_QueryWorkflowRequest"/> message.
        /// </summary>
        Workflow_QueryWorkflowReply = 129,

        /// <summary>
        /// library --> proxy: Returns information about a worflow execution.
        /// </summary>
        Workflow_DescribeWorkflowExecutionRequest = 130,

        /// <summary>
        /// proxy --> library: Sent in response to a <see cref="Workflow_DescribeWorkflowExecutionRequest"/> message.
        /// </summary>
        Workflow_DescribeWorkflowExecutionReply = 131,

        /// <summary>
        /// <b>RESERVED:</b> This is not currently implemented.
        /// </summary>
        [Obsolete("RESERVED but not implemented.")]
        Workflow_DescribeTaskListRequest = 132,

        /// <summary>
        /// <b>RESERVED:</b> This is not currently implemented.
        /// </summary>
        [Obsolete("RESERVED but not implemented.")]
        Workflow_DescribeTaskListReply = 133,

        /// <summary>
        /// proxy --> library: Commands the client library and associated .NET application
        /// to process a workflow instance.
        /// </summary>
        Workflow_InvokeRequest = 134,

        /// <summary>
        /// library --> proxy: Sent in response to a <see cref="Workflow_InvokeRequest"/> message.
        /// </summary>
        Workflow_InvokeReply = 135,

        /// <summary>
        /// proxy --> library: Initiates execution of a child workflow.
        /// </summary>
        Workflow_ExecuteChildRequest = 136,

        /// <summary>
        /// library --> proxy: Sent in response to a <see cref="Workflow_InvokeRequest"/> message.
        /// </summary>
        Workflow_ExecuteChildReply = 137,

        /// <summary>
        /// library --> proxy: Indicates that .NET application wishes to consume signals from
        /// a named channel.  Any signals received by the proxy will be forwarded to the
        /// library via <see cref="Workflow_SignalReceivedRequest"/> messages.
        /// </summary>
        Workflow_SignalSubscribeRequest = 138,

        /// <summary>
        /// proxy --> library: Sent in response to a <see cref="Workflow_SignalSubscribeRequest"/> message.
        /// </summary>
        Workflow_SignalSubscribeReply = 139,

        /// <summary>
        /// proxy --> library: Send when a signal is received by the proxy on a subscribed channel.
        /// </summary>
        Workflow_SignalReceivedRequest = 140,

        /// <summary>
        /// library --> proxy: Sent in response to a <see cref="Workflow_SignalReceivedRequest"/> message.
        /// </summary>
        Workflow_SignalReceivedReply = 141,

        /// <summary>
        /// <para>
        /// proxy --> library: Implements the standard Cadence <i>side effect</i> behavior by 
        /// transmitting a <see cref="Workflow_SideEffectInvokeRequest"/> to the library and
        /// waiting for the <see cref="Workflow_SideEffectInvokeReply"/> reply, persisting the 
        /// answer in the workflow history and then transmitting the answer back to the .NET
        /// workflow implementation via a <see cref="Workflow_SideEffectReply"/>.
        /// </para>
        /// <para>
        /// This message includes a unique identifier that is used to ensure that a specific side effect
        /// operation results in only a single <see cref="Workflow_SideEffectInvokeRequest"/> message to
        /// the .NET workflow application per workflow instance.  Subsequent calls will simply return the
        /// value from the execution history.
        /// </para>
        /// </summary>
        Workflow_SideEffectRequest = 142,

        /// <summary>
        /// library --> proxy: Sent in response to a <see cref="Workflow_SignalReceivedRequest"/> message.
        /// </summary>
        Workflow_SideEffectReply = 143,

        /// <summary>
        /// proxy --> library: Sent by the proxy to the library the first time a side effect
        /// operation is submitted a workflow instance.  The library will response with the
        /// side effect value to be persisted in the workflow history and returned back to
        /// the the .NET workflow application.
        /// </summary>
        Workflow_SideEffectInvokeRequest = 144,

        /// <summary>
        /// library --> proxy: Sent in response to a <see cref="Workflow_SignalReceivedRequest"/> message.
        /// </summary>
        Workflow_SideEffectInvokeReply = 145,

        //---------------------------------------------------------------------
        // Domain messages
        //
        // Note that all domain client request messages will include a [DomainClientId] argument
        // identifying the target domain client.

        /// <summary>
        /// library --> proxy: Registers a Cadence domain.
        /// </summary>
        Domain_RegisterRequest = 200,

        /// <summary>
        /// proxy --> library: Sent in response to a <see cref="Domain_RegisterRequest"/> message.
        /// </summary>
        Domain_RegisterReply = 201,

        /// <summary>
        /// library --> proxy: Describes a Cadence domain.
        /// </summary>
        Domain_DescribeRequest = 202,

        /// <summary>
        /// proxy --> library: Sent in response to a <see cref="Domain_DescribeRequest"/> message.
        /// </summary>
        Domain_DescribeReply = 203,

        /// <summary>
        /// library --> proxy: Updates a Cadence domain.
        /// </summary>
        Domain_UpdateRequest = 204,

        /// <summary>
        /// proxy --> library: Sent in response to a <see cref="Domain_UpdateRequest"/> message.
        /// </summary>
        Domain_UpdateReply = 205,

        //---------------------------------------------------------------------
        // Activity messages

        /// <summary>
        /// proxy --> library: Commands the client library and associated .NET application
        /// to process an activity instance.
        /// </summary>
        Activity_InvokeRequest = 300,

        /// <summary>
        /// library --> proxy: Sent in response to a <see cref="Activity_InvokeRequest"/> message.
        /// </summary>
        Activity_InvokeReply = 301,

        /// <summary>
        /// library --> proxy: Requests the heartbeat details from the last failed attempt.
        /// </summary>
        Activity_GetHeartbeatDetailsRequest = 302,

        /// <summary>
        /// proxy --> library: Sent in response to a <see cref="Activity_GetHeartbeatDetailsRequest"/> message.
        /// </summary>
        Activity_GetHeartbeatDetailsReply = 303,

        /// <summary>
        /// library --> proxy: Logs a message for an activity.
        /// </summary>
        Activity_LogRequest = 304,

        /// <summary>
        /// proxy --> library: Sent in response to a <see cref="Activity_LogRequest"/> message.
        /// </summary>
        Activity_LogReply = 305,

        /// <summary>
        /// library --> proxy: Records a heartbeat message for an activity.
        /// </summary>
        Activity_RecordHeartbeatRequest = 306,

        /// <summary>
        /// proxy --> library: Sent in response to a <see cref="Activity_RecordHeartbeatRequest"/> message.
        /// </summary>
        Activity_RecordHeartbeatReply = 307,

        /// <summary>
        /// library --> proxy: Determines whether an activity execution has any heartbeat details.
        /// </summary>
        Activity_HasHeartbeatDetailsRequest = 308,

        /// <summary>
        /// proxy --> library: Sent in response to a <see cref="Activity_HasHeartbeatDetailsRequest"/> message.
        /// </summary>
        Activity_HasHeartbeatDetailsReply = 309,

        /// <summary>
        /// library --> proxy: Signals that the application executing an activity is terminating,
        /// giving the the proxy a chance to gracefully inform Cadence and then terminate the activity.
        /// </summary>
        Activity_StopRequest = 310,

        /// <summary>
        /// proxy --> library: Sent in response to a <see cref="Activity_StopRequest"/> message.
        /// </summary>
        Activity_StopReply = 311,
    }
}
