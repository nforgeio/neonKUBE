//-----------------------------------------------------------------------------
// FILE:		message_types.go
// CONTRIBUTOR: John C Burns
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

package messagetypes

// MessageType is an enumerated mapping
// of all ProxyMessage types
type MessageType int32

const (

	/// <summary>
	/// Indicates a message with an unspecified type.  This normally indicates an error.
	/// </summary>
	Unspecified MessageType = 0

	//---------------------------------------------------------------------
	// Client messages

	/// <summary>
	/// <b>client --> proxy:</b> Informs the proxy of the network endpoint where the
	/// client is listening for proxy messages.  The proxy should respond with an
	/// <see cref="InitializeReply"/> when it's ready to begin receiving inbound
	/// proxy messages.
	/// </summary>
	InitializeRequest MessageType = 1

	/// <summary>
	/// <b>proxy --> client:</b> Sent in response to a <see cref="InitializeRequest"/> message
	/// to indicate that the proxy ready to begin receiving inbound proxy messages.
	/// </summary>
	InitializeReply MessageType = 2

	/// <summary>
	/// client --> proxy: Requests that the proxy establish a connection to a Cadence
	/// cluster.  This maps to a <c>NewClient()</c> in the proxy.
	/// </summary>
	ConnectRequest MessageType = 3

	/// <summary>
	/// proxy --> client: Sent in response to a <see cref="ConnectRequest"/> message.
	/// </summary>
	ConnectReply MessageType = 4

	/// <summary>
	/// <b>client --> proxy:</b> Signals the proxy that it should terminate gracefully.  The
	/// proxy should send a <see cref="TerminateReply"/> back to the client and
	/// then exit terminating the process.
	/// </summary>
	TerminateRequest MessageType = 5

	/// <summary>
	/// <b>proxy --> client:</b> Sent in response to a <see cref="TerminateRequest"/> message.
	/// </summary>
	TerminateReply MessageType = 6

	/// <summary>
	/// <b>client --> proxy:</b> Requests that the proxy register a Cadence domain.
	/// </summary>
	DomainRegisterRequest MessageType = 7

	/// <summary>
	/// <b>proxy --> client:</b> Sent in response to a <see cref="DomainRegisterRequest"/> message.
	/// </summary>
	DomainRegisterReply MessageType = 8

	/// <summary>
	/// <b>client --> proxy:</b> Requests that the proxy return the details for a Cadence domain.
	/// </summary>
	DomainDescribeRequest MessageType = 9

	/// <summary>
	/// <b>proxy --> client:</b> Sent in response to a <see cref="DomainDescribeRequest"/> message.
	/// </summary>
	DomainDescribeReply MessageType = 10

	/// <summary>
	/// <b>client --> proxy:</b> Requests that the proxy update a Cadence domain.
	/// </summary>
	DomainUpdateRequest MessageType = 11

	/// <summary>
	/// <b>proxy --> client:</b> Sent in response to a <see cref="DomainUpdateRequest"/> message.
	/// </summary>
	DomainUpdateReply MessageType = 12

	/// <summary>
	/// <b>client --> proxy:</b> Sent periodically (every second) by the client to the
	/// proxy to verify that it is still healthy.
	/// </summary>
	HeartbeatRequest MessageType = 13

	/// <summary>
	/// <b>proxy --> client:</b> Sent in response to a <see cref="HeartbeatRequest"/> message.
	/// </summary>
	HeartbeatReply MessageType = 14

	/// <summary>
	/// <b>client --> proxy:</b> Sent to request that a pending operation be cancelled.
	/// </summary>
	CancelRequest MessageType = 15

	/// <summary>
	/// <b>proxy --> client:</b> Sent in response to a <see cref="CancelRequest"/> message
	/// indicating that the operation was canceled or that it already completed or no longer
	/// exists.
	/// </summary>
	CancelReply MessageType = 16

	/// <summary>
	/// <b>client --> proxy:</b> Indicates that the application is capable of handling workflows
	/// and activities within a specific Cadence domain and task lisk.
	/// </summary>
	NewWorkerRequest MessageType = 17

	/// <summary>
	/// <b>proxy --> client:</b> Sent in response to a <see cref="NewWorkerRequest"/> message.
	/// </summary>
	NewWorkerReply MessageType = 18

	/// <summary>
	/// <b>client --> proxy:</b> Stops a Cadence worker.
	/// </summary>
	StopWorkerRequest MessageType = 19

	/// <summary>
	/// <b>proxy --> client:</b> Sent in response to a <see cref="StopWorkerRequest"/> message
	/// </summary>
	StopWorkerReply MessageType = 20

	/// <summary>
	/// Sent from either the client or proxy mainly for measuring the raw throughput of
	/// client/proxy transactions.  The receiver simply responds immediately with a
	/// <see cref="PingReply"/>.
	/// </summary>
	PingRequest MessageType = 21

	/// <summary>
	/// Sent by either side in response to a <see cref="PingRequest"/>.
	/// </summary>
	PingReply MessageType = 22

	/// <summary>
	/// <b>client --> proxy:</b> Requests that the proxy deprecate a Cadence domain.
	/// </summary>
	DomainDeprecateRequest MessageType = 23

	/// <summary>
	/// <b>proxy --> client:</b> Sent in response to a <see cref="DomainDeprecateRequest"/> message.
	/// </summary>
	DomainDeprecateReply MessageType = 24

	//---------------------------------------------------------------------
	// Workflow messages
	//
	// Note that all workflow client request messages will include [WorkflowClientId] property
	// identifying the target workflow client.

	/// <summary>
	/// <b>client --> proxy:</b> Registers a workflow handler.
	/// </summary>
	WorkflowRegisterRequest MessageType = 100

	/// <summary>
	/// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowRegisterRequest"/> message.
	/// </summary>
	WorkflowRegisterReply MessageType = 101

	/// <summary>
	/// <b>client --> proxy:</b> Starts a workflow.
	/// </summary>
	WorkflowExecuteRequest MessageType = 102

	/// <summary>
	/// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowExecuteRequest"/> message.
	/// </summary>
	WorkflowExecuteReply MessageType = 103

	/// <summary>
	/// <b>client --> proxy:</b> Signals a running workflow.
	/// </summary>
	WorkflowSignalRequest MessageType = 104

	/// <summary>
	/// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowSignalRequest"/> message.
	/// </summary>
	WorkflowSignalReply MessageType = 105

	/// <summary>
	///<b>client --> proxy:</b> Signals a workflow starting it first if necessary.
	/// </summary>
	WorkflowSignalWithStartRequest MessageType = 106

	/// <summary>
	/// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowSignalWithStartRequest"/> message.
	/// </summary>
	WorkflowSignalWithStartReply MessageType = 107

	/// <summary>
	/// <b>client --> proxy:</b> Cancels a workflow.
	/// </summary>
	WorkflowCancelRequest MessageType = 108

	/// <summary>
	/// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowCancelRequest"/> message.
	/// </summary>
	WorkflowCancelReply MessageType = 109

	/// <summary>
	/// <b>client --> proxy:</b> Terminates a workflow.
	/// </summary>
	WorkflowTerminateRequest MessageType = 110

	/// <summary>
	/// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowTerminateRequest"/> message.
	/// </summary>
	WorkflowTerminateReply MessageType = 111

	/// <summary>
	/// <b>client --> proxy:</b> Requests a workflow's history.
	/// </summary>
	WorkflowGetHistoryRequest MessageType = 112

	/// <summary>
	/// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowGetHistoryRequest"/> message.
	/// </summary>
	WorkflowGetHistoryReply MessageType = 113

	/// <summary>
	/// <b>client --> proxy:</b> Requests the list of closed workflows.
	/// </summary>
	WorkflowListClosedRequest MessageType = 114

	/// <summary>
	/// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowListClosedRequest"/> message.
	/// </summary>
	WorkflowListClosedReply MessageType = 115

	/// <summary>
	/// <b>client --> proxy:</b> Requests the list of open workflows.
	/// </summary>
	WorkflowListOpenExecutionsRequest MessageType = 116

	/// <summary>
	/// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowListOpenExecutionsRequest"/> message.
	/// </summary>
	WorkflowListOpenExecutionsReply MessageType = 117

	/// <summary>
	/// <b>client --> proxy:</b> Queries a workflow.
	/// </summary>
	WorkflowQueryRequest MessageType = 118

	/// <summary>
	/// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowQueryRequest"/> message.
	/// </summary>
	WorkflowQueryReply MessageType = 119

	/// <summary>
	/// <b>client --> proxy:</b> Returns information about a worflow execution.
	/// </summary>
	WorkflowDescribeExecutionRequest MessageType = 120

	/// <summary>
	/// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowDescribeExecutionRequest"/> message.
	/// </summary>
	WorkflowDescribeExecutionReply MessageType = 121

	/// <summary>
	/// <b>RESERVED:</b> This is not currently implemented.
	/// </summary>
	WorkflowDescribeTaskListRequest MessageType = 122

	/// <summary>
	/// <b>RESERVED:</b> This is not currently implemented.
	/// </summary>
	WorkflowDescribeTaskListReply MessageType = 123

	/// <summary>
	/// <b>proxy --> client:</b> Commands the client client and associated .NET application
	/// to process a workflow instance.
	/// </summary>
	WorkflowInvokeRequest MessageType = 124

	/// <summary>
	/// <b>client --> proxy:</b> Sent in response to a <see cref="WorkflowInvokeRequest"/> message.
	/// </summary>
	WorkflowInvokeReply MessageType = 125

	/// <summary>
	/// <b>client --> proxy:</b> Initiates execution of a child workflow.
	/// </summary>
	WorkflowExecuteChildRequest MessageType = 126

	/// <summary>
	/// <b>proxy --> cl;ient:</b> Sent in response to a <see cref="WorkflowExecuteChildRequest"/> message.
	/// </summary>
	WorkflowExecuteChildReply MessageType = 127

	/// <summary>
	/// <b>client --> proxy:</b> Indicates that .NET application wishes to consume signals from
	/// a named channel.  Any signals received by the proxy will be forwarded to the
	/// client via <see cref="WorkflowSignalInvokeRequest"/> messages.
	/// </summary>
	WorkflowSignalSubscribeRequest MessageType = 128

	/// <summary>
	/// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowSignalSubscribeRequest"/> message.
	/// </summary>
	WorkflowSignalSubscribeReply MessageType = 129

	/// <summary>
	/// <b>proxy --> client:</b> Sent when a signal is received by the proxy on a subscribed channel.
	/// </summary>
	WorkflowSignalInvokeRequest MessageType = 130

	/// <summary>
	/// <b>client --> proxy:</b> Sent in response to a <see cref="WorkflowSignalInvokeRequest"/> message.
	/// </summary>
	WorkflowSignalInvokeReply MessageType = 131

	/// <summary>
	/// <b>client --> proxy:</b> Implements the standard Cadence <i>side effect</i> behavior
	/// by including the mutable result being set.
	/// </summary>
	WorkflowMutableRequest MessageType = 132

	/// <summary>
	/// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowMutableRequest"/> message.
	/// </summary>
	WorkflowMutableReply MessageType = 133

	/// <summary>
	/// <b>client --> proxy:</b> Manages workflow versioning.
	/// </summary>
	WorkflowGetVersionRequest MessageType = 134

	/// <summary>
	/// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowGetVersionRequest"/> message.
	/// </summary>
	WorkflowGetVersionReply MessageType = 135

	/// <summary>
	/// <b>client --> proxy:</b> Sets the maximum number of bytes the client will use
	/// to cache the history of a sticky workflow on a workflow worker as a performance
	/// optimization.  When this is exceeded for a workflow its full history will
	/// need to be retrieved from the Cadence cluster the next time the workflow
	/// instance is assigned to a worker.
	/// </summary>
	WorkflowSetCacheSizeRequest MessageType = 136

	/// <summary>
	/// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowSetCacheSizeRequest"/>.
	/// </summary>
	WorkflowSetCacheSizeReply MessageType = 137

	/// <summary>
	/// <b>client --> proxy:</b> Requests the workflow result encoded as a byte array waiting
	/// for the workflow to complete if it is still running.  Note that this request will fail
	/// if the workflow did not run to completion.
	/// </summary>
	WorkflowGetResultRequest MessageType = 138

	/// <summary>
	/// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowGetResultRequest"/>.
	/// </summary>
	WorkflowGetResultReply MessageType = 139

	/// <summary>
	///  <b>client --> proxy:</b> Determines whether the last execution of the workflow has
	///  a completion result.  This can be used by CRON workflows to determine whether the
	///  last execution returned a result.
	/// </summary>
	WorkflowHasLastResultRequest MessageType = 140

	/// <summary>
	/// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowHasLastResultRequest"/>.
	/// </summary>
	WorkflowHasLastResultReply MessageType = 141

	/// <summary>
	///  <b>client --> proxy:</b> Returns the result from the last execution of the workflow.
	///  This can be used by CRON workflows to retrieve state from the last workflow execution.
	/// </summary>
	WorkflowGetLastResultRequest MessageType = 142

	/// <summary>
	/// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowGetLastResultRequest"/>.
	/// </summary>
	WorkflowGetLastResultReply MessageType = 143

	/// <summary>
	///  <b>client --> proxy:</b> Commands the proxy to replace the current workflow context
	///  with a new disconnected context.
	/// </summary>
	WorkflowDisconnectContextRequest MessageType = 144

	/// <summary>
	/// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowDisconnectContextRequest"/>.
	/// </summary>
	WorkflowDisconnectContextReply MessageType = 145

	/// <summary>
	/// <b>client --> proxy:</b> Request the current workflow time (UTC).
	/// </summary>
	WorkflowGetTimeRequest MessageType = 146

	/// <summary>
	/// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowGetTimeRequest"/>.
	/// </summary>
	WorkflowGetTimeReply MessageType = 147

	/// <summary>
	/// <b>client --> proxy:</b> Sent to have the workflow sleep for a period of time.
	/// </summary>
	WorkflowSleepRequest MessageType = 148

	/// <summary>
	/// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowSleepRequest"/>.
	/// </summary>
	WorkflowSleepReply MessageType = 149

	/// <summary>
	/// <b>client --> proxy:</b> Waits for a workflow that has already been started
	/// by a <see cref="WorkflowExecuteChildRequest"/> to finish.
	/// </summary>
	WorkflowWaitForChildRequest MessageType = 150

	/// <summary>
	/// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowWaitForChildRequest"/> message
	/// after the child is finish.
	/// </summary>
	WorkflowWaitForChildReply MessageType = 151

	/// <summary>
	/// <b>client --> proxy:</b> Sends a signal to a child workflow.
	/// </summary>
	WorkflowSignalChildRequest MessageType = 152

	/// <summary>
	/// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowSignalChildRequest"/> message.
	/// </summary>
	WorkflowSignalChildReply MessageType = 153

	/// <summary>
	/// <b>client --> proxy:</b> Cancels a child workflow.
	/// </summary>
	WorkflowCancelChildRequest MessageType = 154

	/// <summary>
	/// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowCancelChildRequest"/> message.
	/// </summary>
	WorkflowCancelChildReply MessageType = 155

	/// <summary>
	/// <b>UNUSED:</b> Available message ID.
	/// </summary>
	UNUSED_2 MessageType = 156

	/// <summary>
	/// <b>UNUSED:</b> Available message ID..
	/// </summary>
	UNUSED_3 MessageType = 157

	/// <summary>
	/// <b>client --> proxy:</b> Registers a query handler by name.
	/// </summary>
	WorkflowSetQueryHandlerRequest MessageType = 158

	/// <summary>
	/// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowSetQueryHandlerRequest"/> message.
	/// </summary>
	WorkflowSetQueryHandlerReply MessageType = 159

	/// <summary>
	/// <b>proxy --> client:</b> Invokes a query on a workflow.
	/// </summary>
	WorkflowQueryInvokeRequest MessageType = 160

	/// <summary>
	/// <b>client --> proxy:</b> Sent in response to a <see cref="WorkflowQueryInvokeRequest"/>.
	/// </summary>
	WorkflowQueryInvokeReply MessageType = 161

	//---------------------------------------------------------------------
	// Activity messages

	/// <summary>
	/// <b>client --> proxy:</b> Executes an activity within the context of a workflow.
	/// </summary>
	ActivityExecuteRequest MessageType = 200

	/// <summary>
	/// <b>proxy --> client:</b> Sent in response to a <see cref="ActivityExecuteRequest"/> message.
	/// </summary>
	ActivityExecuteReply MessageType = 201

	/// <summary>
	/// <b>proxy --> client:</b> Invokes an activity on an activity worker.
	/// </summary>
	ActivityInvokeRequest MessageType = 202

	/// <summary>
	/// <b>client --> proxy:</b> Sent in response to a <see cref="ActivityInvokeRequest"/> message.
	/// </summary>
	ActivityInvokeReply MessageType = 203

	/// <summary>
	/// <b>client --> proxy:</b> Requests the heartbeat details from the last failed activity execution.
	/// </summary>
	ActivityGetHeartbeatDetailsRequest MessageType = 204

	/// <summary>
	/// <b>proxy --> client:</b> Sent in response to a <see cref="ActivityGetHeartbeatDetailsRequest"/> message.
	/// </summary>
	ActivityGetHeartbeatDetailsReply MessageType = 205

	/// <summary>
	/// <b>client --> proxy:</b> Logs a message for an activity.
	/// </summary>
	ActivityLogRequest MessageType = 206

	/// <summary>
	/// <b>proxy --> client:</b> Sent in response to a <see cref="ActivityLogRequest"/> message.
	/// </summary>
	ActivityLogReply MessageType = 207

	/// <summary>
	/// <b>client --> proxy:</b> Records a heartbeat message for an activity.
	/// </summary>
	ActivityRecordHeartbeatRequest MessageType = 208

	/// <summary>
	/// <b>proxy --> client:</b> Sent in response to a <see cref="ActivityRecordHeartbeatRequest"/> message.
	/// </summary>
	ActivityRecordHeartbeatReply MessageType = 209

	/// <summary>
	/// <b>client --> proxy:</b> Determines whether an activity execution has any heartbeat details.
	/// </summary>
	ActivityHasHeartbeatDetailsRequest MessageType = 210

	/// <summary>
	/// <b>proxy --> client:</b> Sent in response to a <see cref="ActivityHasHeartbeatDetailsRequest"/> message.
	/// </summary>
	ActivityHasHeartbeatDetailsReply MessageType = 211

	/// <summary>
	/// <b>proxy --> client:</b> Signals the client that an activity is being stopped.
	/// </summary>
	ActivityStoppingRequest MessageType = 212

	/// <summary>
	/// <b>client --> proxy:</b> Sent in response to a <see cref="ActivityStoppingRequest"/> message.
	/// </summary>
	ActivityStoppingReply MessageType = 213

	/// <summary>
	/// <b>client --> proxy:</b> Executes a local activity within the context of a workflow.
	/// </summary>
	ActivityExecuteLocalRequest MessageType = 214

	/// <summary>
	/// <b>proxy --> client:</b> Sent in response to a <see cref="ActivityExecuteLocalRequest"/> message.
	/// </summary>
	ActivityExecuteLocalReply MessageType = 215

	/// <summary>
	/// <b>proxy --> client:</b> Invokes a local activity on an activity worker.
	/// </summary>
	ActivityInvokeLocalRequest MessageType = 216

	/// <summary>
	/// <b>client --> proxy:</b> Sent in response to a <see cref="ActivityInvokeLocalRequest"/> message.
	/// </summary>
	ActivityInvokeLocalReply MessageType = 217

	/// <summary>
	/// <b>client --> proxy:</b> Registers an activity handler.
	/// </summary>
	ActivityRegisterRequest MessageType = 218

	/// <summary>
	/// <b>proxy --> client:</b> Sent in response to an <see cref="ActivityRegisterRequest"/> message.
	/// </summary>
	ActivityRegisterReply MessageType = 219

	/// <summary>
	/// <b>client --> proxy:</b> Requests information about an activity.
	/// </summary>
	ActivityGetInfoRequest MessageType = 220

	/// <summary>
	/// <b>proxy --> client:</b> Sent in response to an <see cref="ActivityGetInfoRequest"/> message.
	/// </summary>
	ActivityGetInfoReply MessageType = 221

	/// <summary>
	/// <b>client --> proxy:</b> Requests that an activity be completed externally.
	/// </summary>
	ActivityCompleteRequest MessageType = 222

	/// <summary>
	/// <b>proxy --> client:</b> Sent in response to an <see cref="ActivityCompleteRequest"/> message.
	/// </summary>
	ActivityCompleteReply MessageType = 223
)

// String translates a message type enum into
// the corresponding string
func (m MessageType) String() string {
	switch m {
	case Unspecified:
		return "Unspecified"
	case InitializeRequest:
		return "InitializeRequest"
	case InitializeReply:
		return "InitializeReply"
	case ConnectRequest:
		return "ConnectRequest"
	case ConnectReply:
		return "ConnectReply"
	case TerminateRequest:
		return "TerminateRequest"
	case TerminateReply:
		return "TerminateReply"
	case DomainRegisterRequest:
		return "DomainRegisterRequest"
	case DomainRegisterReply:
		return "DomainRegisterReply"
	case DomainDeprecateRequest:
		return "DomainDeprecateRequest"
	case DomainDeprecateReply:
		return "DomainDeprecateReply"
	case DomainDescribeRequest:
		return "DomainDescribeRequest"
	case DomainDescribeReply:
		return "DomainDescribeReply"
	case DomainUpdateRequest:
		return "DomainUpdateRequest"
	case DomainUpdateReply:
		return "DomainUpdateReply"
	case HeartbeatRequest:
		return "HeartbeatRequest"
	case HeartbeatReply:
		return "HeartbeatReply"
	case CancelRequest:
		return "CancelRequest"
	case CancelReply:
		return "CancelReply"
	case NewWorkerRequest:
		return "NewWorkerRequest"
	case NewWorkerReply:
		return "NewWorkerReply"
	case StopWorkerRequest:
		return "StopWorkerRequest"
	case StopWorkerReply:
		return "StopWorkerReply"
	case PingRequest:
		return "PingRequest"
	case PingReply:
		return "PingReply"
	case WorkflowRegisterRequest:
		return "WorkflowRegisterRequest"
	case WorkflowRegisterReply:
		return "WorkflowRegisterReply"
	case WorkflowExecuteRequest:
		return "WorkflowExecuteRequest"
	case WorkflowExecuteReply:
		return "WorkflowExecuteReply"
	case WorkflowSignalRequest:
		return "WorkflowSignalRequest"
	case WorkflowSignalReply:
		return "WorkflowSignalReply"
	case WorkflowSignalWithStartRequest:
		return "WorkflowSignalWithStartRequest"
	case WorkflowSignalWithStartReply:
		return "WorkflowSignalWithStartReply"
	case WorkflowCancelRequest:
		return "WorkflowCancelRequest"
	case WorkflowCancelReply:
		return "WorkflowCancelReply"
	case WorkflowTerminateRequest:
		return "WorkflowTerminateRequest"
	case WorkflowTerminateReply:
		return "WorkflowTerminateReply"
	case WorkflowGetHistoryRequest:
		return "WorkflowGetHistoryRequest"
	case WorkflowGetHistoryReply:
		return "WorkflowGetHistoryReply"
	case WorkflowListClosedRequest:
		return "WorkflowListClosedRequest"
	case WorkflowListClosedReply:
		return "WorkflowListClosedReply"
	case WorkflowListOpenExecutionsRequest:
		return "WorkflowListOpenExecutionRequest"
	case WorkflowListOpenExecutionsReply:
		return "WorkflowListOpenExecutionReply"
	case WorkflowQueryRequest:
		return "WorkflowQueryRequest"
	case WorkflowQueryReply:
		return "WorkflowQueryReply"
	case WorkflowDescribeExecutionRequest:
		return "WorkflowDescribeExecutionRequest"
	case WorkflowDescribeExecutionReply:
		return "WorkflowDescribeExecutionReply"
	case WorkflowDescribeTaskListRequest:
		return "WorkflowDescribeTaskListRequest"
	case WorkflowDescribeTaskListReply:
		return "WorkflowDescribeTaskListReply"
	case WorkflowInvokeRequest:
		return "WorkflowInvokeRequest"
	case WorkflowInvokeReply:
		return "WorkflowInvokeReply"
	case WorkflowExecuteChildRequest:
		return "WorkflowExecuteChildRequest"
	case WorkflowExecuteChildReply:
		return "WorkflowExecuteChildReply"
	case WorkflowSignalSubscribeRequest:
		return "WorkflowSignalSubscribeRequest"
	case WorkflowSignalSubscribeReply:
		return "WorkflowSignalSubscribeReply"
	case WorkflowSignalInvokeRequest:
		return "WorkflowSignalInvokeRequest"
	case WorkflowSignalInvokeReply:
		return "WorkflowSignalInvokeReply"
	case WorkflowMutableRequest:
		return "WorkflowMutableRequest"
	case WorkflowMutableReply:
		return "WorkflowMutableReply"
	case WorkflowGetVersionRequest:
		return "WorkflowGetVersionRequest"
	case WorkflowGetVersionReply:
		return "WorkflowGetVersionReply"
	case WorkflowSetCacheSizeRequest:
		return "WorkflowSetCacheSizeRequest"
	case WorkflowSetCacheSizeReply:
		return "WorkflowSetCacheSizeReply"
	case WorkflowGetResultRequest:
		return "WorkflowGetResultRequest"
	case WorkflowGetResultReply:
		return "WorkflowGetResultReply"
	case WorkflowHasLastResultRequest:
		return "WorkflowHasLastResultRequest"
	case WorkflowHasLastResultReply:
		return "WorkflowHasLastResultReply"
	case WorkflowGetLastResultRequest:
		return "WorkflowGetLastResultRequest"
	case WorkflowGetLastResultReply:
		return "WorkflowGetLastResultReply"
	case WorkflowDisconnectContextRequest:
		return "WorkflowDisconnectContextRequest"
	case WorkflowDisconnectContextReply:
		return "WorkflowDisconnectContextReply"
	case WorkflowGetTimeRequest:
		return "WorkflowGetTimeRequest"
	case WorkflowGetTimeReply:
		return "WorkflowGetTimeReply"
	case WorkflowSleepRequest:
		return "WorkflowSleepRequest"
	case WorkflowSleepReply:
		return "WorkflowSleepReply"
	case WorkflowWaitForChildRequest:
		return "WorkflowWaitForChildRequest"
	case WorkflowWaitForChildReply:
		return "WorkflowWaitForChildReply"
	case WorkflowSignalChildRequest:
		return "WorkflowSignalChildRequest"
	case WorkflowSignalChildReply:
		return "WorkflowSignalChildReply"
	case WorkflowCancelChildRequest:
		return "WorkflowCancelChildRequest"
	case WorkflowCancelChildReply:
		return "WorkflowCancelChildReply"
	case WorkflowSetQueryHandlerRequest:
		return "WorkflowSetQueryHandlerRequest"
	case WorkflowSetQueryHandlerReply:
		return "WorkflowSetQueryHandlerReply"
	case WorkflowQueryInvokeRequest:
		return "WorkflowQueryInvokeRequest"
	case WorkflowQueryInvokeReply:
		return "WorkflowQueryInvokeReply"
	case ActivityExecuteRequest:
		return "ActivityExecuteRequest"
	case ActivityExecuteReply:
		return "ActivityExecuteReply"
	case ActivityInvokeRequest:
		return "ActivityInvokeRequest"
	case ActivityInvokeReply:
		return "ActivityInvokeReply"
	case ActivityGetHeartbeatDetailsRequest:
		return "ActivityGetHeartbeatDetailsRequest"
	case ActivityGetHeartbeatDetailsReply:
		return "ActivityGetHeartbeatDetailsReply"
	case ActivityLogRequest:
		return "ActivityLogRequest"
	case ActivityLogReply:
		return "ActivityLogReply"
	case ActivityRecordHeartbeatRequest:
		return "ActivityRecordHeartbeatRequest"
	case ActivityRecordHeartbeatReply:
		return "ActivityRecordHeartbeatReply"
	case ActivityHasHeartbeatDetailsRequest:
		return "ActivityHasHeartbeatDetailsRequest"
	case ActivityHasHeartbeatDetailsReply:
		return "ActivityHasHeartbeatDetailsReply"
	case ActivityStoppingRequest:
		return "ActivityStoppingRequest"
	case ActivityStoppingReply:
		return "ActivityStoppingReply"
	case ActivityExecuteLocalRequest:
		return "ActivityExecuteLocalRequest"
	case ActivityExecuteLocalReply:
		return "ActivityExecuteLocalReply"
	case ActivityInvokeLocalRequest:
		return "ActivityInvokeLocalRequest"
	case ActivityInvokeLocalReply:
		return "ActivityInvokeLocalReply"
	case ActivityRegisterRequest:
		return "ActivityRegisterRequest"
	case ActivityRegisterReply:
		return "ActivityRegisterReply"
	case ActivityGetInfoRequest:
		return "ActivityGetInfoRequest"
	case ActivityGetInfoReply:
		return "ActivityGetInfoReply"
	case ActivityCompleteRequest:
		return "ActivityCompleteRequest"
	case ActivityCompleteReply:
		return "ActivityCompleteReply"
	default:
		return ""
	}
}
