//-----------------------------------------------------------------------------
// FILE:	    InternalMessageTypes.cs
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

using Neon.Cadence;
using Neon.Common;

namespace Neon.Cadence.Internal
{
    /// <summary>
    /// <b>INTERNAL USE ONLY:</b> Enumerates the possible message types.
    /// </summary>
    internal enum InternalMessageTypes
    {
        /// <summary>
        /// Indicates a message with an unspecified type.  This normally indicates an error.
        /// </summary>
        Unspecified = 0,

        //---------------------------------------------------------------------
        // Client messages

        /// <summary>
        /// <b>client --> proxy:</b> Informs the proxy of the network endpoint where the
        /// client is listening for proxy messages.  The proxy should respond with an
        /// <see cref="InitializeReply"/> when it's ready to begin receiving inbound
        /// proxy messages.
        /// </summary>
        InitializeRequest = 1,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="InitializeRequest"/> message
        /// to indicate that the proxy ready to begin receiving inbound proxy messages.
        /// </summary>
        InitializeReply = 2,

        /// <summary>
        /// client --> proxy: Requests that the proxy establish a connection to a Cadence
        /// cluster.  This maps to a <c>NewClient()</c> in the proxy.
        /// </summary>
        ConnectRequest = 3,

        /// <summary>
        /// proxy --> client: Sent in response to a <see cref="ConnectRequest"/> message.
        /// </summary>
        ConnectReply = 4,

        /// <summary>
        /// <b>client --> proxy:</b> Signals the proxy that it should terminate gracefully.  The
        /// proxy should send a <see cref="TerminateReply"/> back to the client and
        /// then exit, terminating the process.
        /// </summary>
        TerminateRequest = 5,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="TerminateRequest"/> message.
        /// </summary>
        TerminateReply = 6,

        /// <summary>
        /// <b>client --> proxy:</b> Requests that the proxy register a Cadence domain.
        /// </summary>
        DomainRegisterRequest = 7,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="DomainRegisterRequest"/> message.
        /// </summary>
        DomainRegisterReply = 8,

        /// <summary>
        /// <b>client --> proxy:</b> Requests that the proxy return the details for a Cadence domain.
        /// </summary>
        DomainDescribeRequest = 9,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="DomainDescribeRequest"/> message.
        /// </summary>
        DomainDescribeReply = 10,

        /// <summary>
        /// <b>client --> proxy:</b> Requests that the proxy update a Cadence domain.
        /// </summary>
        DomainUpdateRequest = 11,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="DomainUpdateRequest"/> message.
        /// </summary>
        DomainUpdateReply = 12,

        /// <summary>
        /// <b>client --> proxy:</b> Sent periodically (every second) by the client to the
        /// proxy to verify that it is still healthy.
        /// </summary>
        HeartbeatRequest = 13,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="HeartbeatRequest"/> message.
        /// </summary>
        HeartbeatReply = 14,

        /// <summary>
        /// <b>client --> proxy:</b> Sent to request that a pending operation be cancelled.
        /// </summary>
        CancelRequest = 15,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="CancelRequest"/> message,
        /// indicating that the operation was canceled or that it already completed or no longer
        /// exists.
        /// </summary>
        CancelReply = 16,

        /// <summary>
        /// <b>client --> proxy:</b> Indicates that the application is capable of handling workflows
        /// and activities within a specific Cadence domain and task lisk.
        /// </summary>
        NewWorkerRequest = 17,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="NewWorkerRequest"/> message.
        /// </summary>
        NewWorkerReply = 18,

        /// <summary>
        /// <b>client --> proxy:</b> Stops a Cadence worker.
        /// </summary>
        StopWorkerRequest = 19,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="StopWorkerRequest"/> message,
        /// </summary>
        StopWorkerReply = 20,

        /// <summary>
        /// Sent from either the client or proxy mainly for measuring the raw throughput of 
        /// client/proxy transactions.  The receiver simply responds immediately with a
        /// <see cref="PingReply"/>.
        /// </summary>
        PingRequest = 21,

        /// <summary>
        /// Sent by either side in response to a <see cref="PingRequest"/>.
        /// </summary>
        PingReply = 22,

        /// <summary>
        /// <b>client --> proxy:</b> Requests that the proxy deprecate a Cadence domain.
        /// </summary>
        DomainDeprecateRequest = 23,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="DomainDeprecateRequest"/> message.
        /// </summary>
        DomainDeprecateReply = 24,

        //---------------------------------------------------------------------
        // Workflow messages
        //
        // Note that all workflow client request messages will include [WorkflowClientId] property
        // identifying the target workflow client.

        /// <summary>
        /// <b>client --> proxy:</b> Registers a workflow handler.
        /// </summary>
        WorkflowRegisterRequest = 100,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowRegisterRequest"/> message.
        /// </summary>
        WorkflowRegisterReply = 101,

        /// <summary>
        /// <b>client --> proxy:</b> Starts a workflow.
        /// </summary>
        WorkflowExecuteRequest = 102,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowExecuteRequest"/> message.
        /// </summary>
        WorkflowExecuteReply = 103,

        /// <summary>
        /// <b>client --> proxy:</b> Signals a running workflow.
        /// </summary>
        WorkflowSignalRequest = 104,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowSignalRequest"/> message.
        /// </summary>
        WorkflowSignalReply = 105,

        /// <summary>
        ///<b>client --> proxy:</b> Signals a workflow, starting it first if necessary.
        /// </summary>
        WorkflowSignalWithStartRequest = 106,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowSignalWithStartRequest"/> message.
        /// </summary>
        WorkflowSignalWithStartReply = 107,

        /// <summary>
        /// <b>client --> proxy:</b> Cancels a workflow.
        /// </summary>
        WorkflowCancelRequest = 108,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowCancelRequest"/> message.
        /// </summary>
        WorkflowCancelReply = 109,

        /// <summary>
        /// <b>client --> proxy:</b> Terminates a workflow.
        /// </summary>
        WorkflowTerminateRequest = 110,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowTerminateRequest"/> message.
        /// </summary>
        WorkflowTerminateReply = 111,

        /// <summary>
        /// <b>client --> proxy:</b> Requests a workflow's history.
        /// </summary>
        WorkflowGetHistoryRequest = 112,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowGetHistoryRequest"/> message.
        /// </summary>
        WorkflowGetHistoryReply = 113,

        /// <summary>
        /// <b>client --> proxy:</b> Requests the list of closed workflows.
        /// </summary>
        WorkflowListClosedRequest = 114,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowListClosedRequest"/> message.
        /// </summary>
        WorkflowListClosedReply = 115,

        /// <summary>
        /// <b>client --> proxy:</b> Requests the list of open workflows.
        /// </summary>
        WorkflowListOpenExecutionsRequest = 116,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowListOpenExecutionsRequest"/> message.
        /// </summary>
        WorkflowListOpenExecutionsReply = 117,

        /// <summary>
        /// <b>client --> proxy:</b> Queries a workflow.
        /// </summary>
        WorkflowQueryRequest = 118,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowQueryRequest"/> message.
        /// </summary>
        WorkflowQueryReply = 119,

        /// <summary>
        /// <b>client --> proxy:</b> Returns information about a worflow execution.
        /// </summary>
        WorkflowDescribeExecutionRequest = 120,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowDescribeExecutionRequest"/> message.
        /// </summary>
        WorkflowDescribeExecutionReply = 121,

        /// <summary>
        /// <b>RESERVED:</b> This is not currently implemented.
        /// </summary>
        [Obsolete("RESERVED but not implemented.")]
        WorkflowDescribeTaskListRequest = 122,

        /// <summary>
        /// <b>RESERVED:</b> This is not currently implemented.
        /// </summary>
        [Obsolete("RESERVED but not implemented.")]
        WorkflowDescribeTaskListReply = 123,

        /// <summary>
        /// <b>proxy --> client:</b> Commands the client client and associated .NET application
        /// to process a workflow instance.
        /// </summary>
        WorkflowInvokeRequest = 124,

        /// <summary>
        /// <b>client --> proxy:</b> Sent in response to a <see cref="WorkflowInvokeRequest"/> message.
        /// </summary>
        WorkflowInvokeReply = 125,

        /// <summary>
        /// <b>client --> proxy:</b> Initiates execution of a child workflow.
        /// </summary>
        WorkflowExecuteChildRequest = 126,

        /// <summary>
        /// <b>proxy --> cl;ient:</b> Sent in response to a <see cref="WorkflowExecuteChildRequest"/> message.
        /// </summary>
        WorkflowExecuteChildReply = 127,

        /// <summary>
        /// <b>client --> proxy:</b> Indicates that .NET application wishes to consume signals from
        /// a named channel.  Any signals received by the proxy will be forwarded to the
        /// client via <see cref="WorkflowSignalInvokeRequest"/> messages.
        /// </summary>
        WorkflowSignalSubscribeRequest = 128,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowSignalSubscribeRequest"/> message.
        /// </summary>
        WorkflowSignalSubscribeReply = 129,

        /// <summary>
        /// <b>proxy --> client:</b> Sent when a signal is received by the proxy on a subscribed channel.
        /// </summary>
        WorkflowSignalInvokeRequest = 130,

        /// <summary>
        /// <b>client --> proxy:</b> Sent in response to a <see cref="WorkflowSignalInvokeRequest"/> message.
        /// </summary>
        WorkflowSignalInvokeReply = 131,

        /// <summary>
        /// <b>client --> proxy:</b> Implements the standard Cadence <i>side effect</i> behavior
        /// by including the mutable result being set.
        /// </summary>
        [Obsolete("This was replaced by a local activity.")]
        WorkflowMutableRequest = 132,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowMutableRequest"/> message.
        /// </summary>
        [Obsolete("This was replaced by a local activity.")]
        WorkflowMutableReply = 133,

        /// <summary>
        /// <b>client --> proxy:</b> Manages workflow versioning.
        /// </summary>
        WorkflowGetVersionRequest = 134,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowGetVersionRequest"/> message.
        /// </summary>
        WorkflowGetVersionReply = 135,

        /// <summary>
        /// <b>client --> proxy:</b> Sets the maximum number of bytes the client will use
        /// to cache the history of a sticky workflow on a workflow worker as a performance
        /// optimization.  When this is exceeded for a workflow, its full history will
        /// need to be retrieved from the Cadence cluster the next time the workflow
        /// instance is assigned to a worker. 
        /// </summary>
        WorkflowSetCacheSizeRequest = 136,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowSetCacheSizeRequest"/>.
        /// </summary>
        WorkflowSetCacheSizeReply = 137,

        /// <summary>
        /// <b>client --> proxy:</b> Requests the workflow result encoded as a byte array, waiting
        /// for the workflow to complete if it is still running.  Note that this request will fail
        /// if the workflow did not run to completion.
        /// </summary>
        WorkflowGetResultRequest = 138,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowGetResultRequest"/>.
        /// </summary>
        WorkflowGetResultReply = 139,

        /// <summary>
        ///  <b>client --> proxy:</b> Determines whether the last execution of the workflow has
        ///  a completion result.  This can be used by CRON workflows to determine whether the
        ///  last run returned a result.
        /// </summary>
        WorkflowHasLastResultRequest = 140,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowHasLastResultRequest"/>.
        /// </summary>
        WorkflowHasLastResultReply = 141,

        /// <summary>
        ///  <b>client --> proxy:</b> Returns the result from the last execution of the workflow.
        ///  This can be used by CRON workflows to retrieve state from the last workflow run.
        /// </summary>
        WorkflowGetLastResultRequest = 142,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowGetLastResultRequest"/>.
        /// </summary>
        WorkflowGetLastResultReply = 143,

        /// <summary>
        ///  <b>client --> proxy:</b> Commands the proxy to replace the current workflow context
        ///  with a new disconnected context.
        /// </summary>
        WorkflowDisconnectContextRequest = 144,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowDisconnectContextRequest"/>.
        /// </summary>
        WorkflowDisconnectContextReply = 145,

        /// <summary>
        /// <b>client --> proxy:</b> Request the current workflow time (UTC).
        /// </summary>
        WorkflowGetTimeRequest = 146,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowGetTimeRequest"/>.
        /// </summary>
        WorkflowGetTimeReply = 147,

        /// <summary>
        /// <b>client --> proxy:</b> Sent to have the workflow sleep for a period of time.
        /// </summary>
        WorkflowSleepRequest = 148,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowSleepRequest"/>.
        /// </summary>
        WorkflowSleepReply = 149,

        /// <summary>
        /// <b>client --> proxy:</b> Waits for a workflow that has already been started
        /// by a <see cref="WorkflowExecuteChildRequest"/> to finish.
        /// </summary>
        WorkflowWaitForChildRequest = 150,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowWaitForChildRequest"/> message
        /// after the child is finish.
        /// </summary>
        WorkflowWaitForChildReply = 151,

        /// <summary>
        /// <b>client --> proxy:</b> Sends a signal to a child workflow.
        /// </summary>
        WorkflowSignalChildRequest = 152,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowSignalChildRequest"/> message.
        /// </summary>
        WorkflowSignalChildReply = 153,

        /// <summary>
        /// <b>client --> proxy:</b> Cancels a child workflow.
        /// </summary>
        WorkflowCancelChildRequest = 154,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowCancelChildRequest"/> message.
        /// </summary>
        WorkflowCancelChildReply = 155,

        /// <summary>
        /// <b>UNUSED:</b> Available message ID.
        /// </summary>
        UNUSED_2 = 156,

        /// <summary>
        /// <b>UNUSED:</b> Available message ID..
        /// </summary>
        UNUSED_3 = 157,

        /// <summary>
        /// <b>client --> proxy:</b> Registers a query handler by name.
        /// </summary>
        WorkflowSetQueryHandlerRequest = 158,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowSetQueryHandlerRequest"/> message.
        /// </summary>
        WorkflowSetQueryHandlerReply = 159,

        /// <summary>
        /// <b>proxy --> client:</b> Invokes a query on a workflow.
        /// </summary>
        WorkflowQueryInvokeRequest = 160,

        /// <summary>
        /// <b>client --> proxy:</b> Sent in response to a <see cref="WorkflowQueryInvokeRequest"/>.
        /// </summary>
        WorkflowQueryInvokeReply = 161,

        //---------------------------------------------------------------------
        // Activity messages

        /// <summary>
        /// <b>client --> proxy:</b> Executes an activity within the context of a workflow.
        /// </summary>
        ActivityExecuteRequest = 200,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="ActivityExecuteRequest"/> message.
        /// </summary>
        ActivityExecuteReply = 201,

        /// <summary>
        /// <b>proxy --> client:</b> Invokes an activity on an activity worker. 
        /// </summary>
        ActivityInvokeRequest = 202,

        /// <summary>
        /// <b>client --> proxy:</b> Sent in response to a <see cref="ActivityInvokeRequest"/> message.
        /// </summary>
        ActivityInvokeReply = 203,

        /// <summary>
        /// <b>client --> proxy:</b> Requests the heartbeat details from the last failed activity run.
        /// </summary>
        ActivityGetHeartbeatDetailsRequest = 204,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="ActivityGetHeartbeatDetailsRequest"/> message.
        /// </summary>
        ActivityGetHeartbeatDetailsReply = 205,

        /// <summary>
        /// <b>client --> proxy:</b> Logs a message for an activity.
        /// </summary>
        ActivityLogRequest = 206,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="ActivityLogRequest"/> message.
        /// </summary>
        ActivityLogReply = 207,

        /// <summary>
        /// <b>client --> proxy:</b> Records a heartbeat message for an activity.
        /// </summary>
        ActivityRecordHeartbeatRequest = 208,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="ActivityRecordHeartbeatRequest"/> message.
        /// </summary>
        ActivityRecordHeartbeatReply = 209,

        /// <summary>
        /// <b>client --> proxy:</b> Determines whether an activity execution has any heartbeat details.
        /// </summary>
        ActivityHasHeartbeatDetailsRequest = 210,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="ActivityHasHeartbeatDetailsRequest"/> message.
        /// </summary>
        ActivityHasHeartbeatDetailsReply = 211,

        /// <summary>
        /// <b>proxy --> client:</b> Signals the client that an activity is being stopped. 
        /// </summary>
        ActivityStoppingRequest = 212,

        /// <summary>
        /// <b>client --> proxy:</b> Sent in response to a <see cref="ActivityStoppingRequest"/> message.
        /// </summary>
        ActivityStoppingReply = 213,

        /// <summary>
        /// <b>client --> proxy:</b> Executes a local activity within the context of a workflow.
        /// </summary>
        ActivityExecuteLocalRequest = 214,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="ActivityExecuteLocalRequest"/> message.
        /// </summary>
        ActivityExecuteLocalReply = 215,

        /// <summary>
        /// <b>proxy --> client:</b> Invokes a local activity on an activity worker. 
        /// </summary>
        ActivityInvokeLocalRequest = 216,

        /// <summary>
        /// <b>client --> proxy:</b> Sent in response to a <see cref="ActivityInvokeLocalRequest"/> message.
        /// </summary>
        ActivityInvokeLocalReply = 217,

        /// <summary>
        /// <b>client --> proxy:</b> Registers an activity handler.
        /// </summary>
        ActivityRegisterRequest = 218,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to an <see cref="ActivityRegisterRequest"/> message.
        /// </summary>
        ActivityRegisterReply = 219,

        /// <summary>
        /// <b>client --> proxy:</b> Requests information about an activity.
        /// </summary>
        ActivityGetInfoRequest = 220,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to an <see cref="ActivityGetInfoRequest"/> message.
        /// </summary>
        ActivityGetInfoReply = 221,

        /// <summary>
        /// <b>client --> proxy:</b> Requests that an activity be completed externally.
        /// </summary>
        ActivityCompleteRequest = 222,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to an <see cref="ActivityCompleteRequest"/> message.
        /// </summary>
        ActivityCompleteReply = 223,
    }
}
