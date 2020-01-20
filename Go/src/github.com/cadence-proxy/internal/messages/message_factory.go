//-----------------------------------------------------------------------------
// FILE:		message_factory.go
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

package messages

import (
	internal "github.com/cadence-proxy/internal"
)

// CreateNewTypedMessage creates newly initialized message of the specified type
//
// param messageType message.MessageType -> the MessageType to be created
//
// returns IProxyMessage -> the initialized message as an IProxyMessage interface
func CreateNewTypedMessage(messageType internal.MessageType) IProxyMessage {
	var message IProxyMessage
	switch messageType {

	// --------------------------------------------------------------------------
	// Client messages

	// Cancel
	case internal.CancelRequest:
		message = NewCancelRequest()
	case internal.CancelReply:
		message = NewCancelReply()

	// Connect
	case internal.ConnectReply:
		message = NewConnectReply()
	case internal.ConnectRequest:
		message = NewConnectRequest()

	// Disconnect
	case internal.DisconnectReply:
		message = NewDisconnectReply()
	case internal.DisconnectRequest:
		message = NewDisconnectRequest()

	// DomainDescribe
	case internal.DomainDescribeReply:
		message = NewDomainDescribeReply()
	case internal.DomainDescribeRequest:
		message = NewDomainDescribeRequest()

	// DomainRegister
	case internal.DomainRegisterReply:
		message = NewDomainRegisterReply()
	case internal.DomainRegisterRequest:
		message = NewDomainRegisterRequest()

	// DomainUpdate
	case internal.DomainUpdateReply:
		message = NewDomainUpdateReply()
	case internal.DomainUpdateRequest:
		message = NewDomainUpdateRequest()

	// DomainDeprecate
	case internal.DomainDeprecateReply:
		message = NewDomainDeprecateReply()
	case internal.DomainDeprecateRequest:
		message = NewDomainDeprecateRequest()

	// DomainList
	case internal.DomainListReply:
		message = NewDomainListReply()
	case internal.DomainListRequest:
		message = NewDomainListRequest()

	// Heartbeat
	case internal.HeartbeatReply:
		message = NewHeartbeatReply()
	case internal.HeartbeatRequest:
		message = NewHeartbeatRequest()

	// Initialize
	case internal.InitializeReply:
		message = NewInitializeReply()
	case internal.InitializeRequest:
		message = NewInitializeRequest()

	// Terminate
	case internal.TerminateReply:
		message = NewTerminateReply()
	case internal.TerminateRequest:
		message = NewTerminateRequest()

	// NewWorker
	case internal.NewWorkerReply:
		message = NewNewWorkerReply()
	case internal.NewWorkerRequest:
		message = NewNewWorkerRequest()

	// StopWorker
	case internal.StopWorkerRequest:
		message = NewStopWorkerRequest()
	case internal.StopWorkerReply:
		message = NewStopWorkerReply()

	// Ping
	case internal.PingReply:
		message = NewPingReply()
	case internal.PingRequest:
		message = NewPingRequest()

	// Log
	case internal.LogReply:
		message = NewLogReply()
	case internal.LogRequest:
		message = NewLogRequest()

	// DescribeTaskList
	case internal.DescribeTaskListReply:
		message = NewDescribeTaskListReply()
	case internal.DescribeTaskListRequest:
		message = NewDescribeTaskListRequest()

	// --------------------------------------------------------------------------
	// Workflow messages

	// WorkflowExecute
	case internal.WorkflowExecuteReply:
		message = NewWorkflowExecuteReply()
	case internal.WorkflowExecuteRequest:
		message = NewWorkflowExecuteRequest()

	// WorkflowInvoke
	case internal.WorkflowInvokeReply:
		message = NewWorkflowInvokeReply()
	case internal.WorkflowInvokeRequest:
		message = NewWorkflowInvokeRequest()

	// WorkflowRegister
	case internal.WorkflowRegisterReply:
		message = NewWorkflowRegisterReply()
	case internal.WorkflowRegisterRequest:
		message = NewWorkflowRegisterRequest()

	// WorkflowCancel
	case internal.WorkflowCancelReply:
		message = NewWorkflowCancelReply()
	case internal.WorkflowCancelRequest:
		message = NewWorkflowCancelRequest()

	// WorkflowSignal
	case internal.WorkflowSignalInvokeRequest:
		message = NewWorkflowSignalInvokeRequest()
	case internal.WorkflowSignalInvokeReply:
		message = NewWorkflowSignalInvokeReply()

	// WorkflowTerminate
	case internal.WorkflowTerminateReply:
		message = NewWorkflowTerminateReply()
	case internal.WorkflowTerminateRequest:
		message = NewWorkflowTerminateRequest()

	// WorkflowSignalWithStart
	case internal.WorkflowSignalWithStartReply:
		message = NewWorkflowSignalWithStartReply()
	case internal.WorkflowSignalWithStartRequest:
		message = NewWorkflowSignalWithStartRequest()

	// WorkflowSetCacheSize
	case internal.WorkflowSetCacheSizeRequest:
		message = NewWorkflowSetCacheSizeRequest()
	case internal.WorkflowSetCacheSizeReply:
		message = NewWorkflowSetCacheSizeReply()

	// WorkflowQuery
	case internal.WorkflowQueryReply:
		message = NewWorkflowQueryReply()
	case internal.WorkflowQueryRequest:
		message = NewWorkflowQueryRequest()

	// WorkflowMutable
	case internal.WorkflowMutableReply:
		message = NewWorkflowMutableReply()
	case internal.WorkflowMutableRequest:
		message = NewWorkflowMutableRequest()

	// WorkflowDescribeExecution
	case internal.WorkflowDescribeExecutionReply:
		message = NewWorkflowDescribeExecutionReply()
	case internal.WorkflowDescribeExecutionRequest:
		message = NewWorkflowDescribeExecutionRequest()

	// WorkflowGetResult
	case internal.WorkflowGetResultRequest:
		message = NewWorkflowGetResultRequest()
	case internal.WorkflowGetResultReply:
		message = NewWorkflowGetResultReply()

	// WorkflowSignalSubscribe
	case internal.WorkflowSignalSubscribeReply:
		message = NewWorkflowSignalSubscribeReply()
	case internal.WorkflowSignalSubscribeRequest:
		message = NewWorkflowSignalSubscribeRequest()

	// WorkflowSignal
	case internal.WorkflowSignalReply:
		message = NewWorkflowSignalReply()
	case internal.WorkflowSignalRequest:
		message = NewWorkflowSignalRequest()

	// WorkflowHasLastResult
	case internal.WorkflowHasLastResultReply:
		message = NewWorkflowHasLastResultReply()
	case internal.WorkflowHasLastResultRequest:
		message = NewWorkflowHasLastResultRequest()

	// WorkflowGetLastResult
	case internal.WorkflowGetLastResultReply:
		message = NewWorkflowGetLastResultReply()
	case internal.WorkflowGetLastResultRequest:
		message = NewWorkflowGetLastResultRequest()

	// WorkflowDisconnectContext
	case internal.WorkflowDisconnectContextReply:
		message = NewWorkflowDisconnectContextReply()
	case internal.WorkflowDisconnectContextRequest:
		message = NewWorkflowDisconnectContextRequest()

	// WorkflowGetTime
	case internal.WorkflowGetTimeReply:
		message = NewWorkflowGetTimeReply()
	case internal.WorkflowGetTimeRequest:
		message = NewWorkflowGetTimeRequest()

	// WorkflowSleep
	case internal.WorkflowSleepReply:
		message = NewWorkflowSleepReply()
	case internal.WorkflowSleepRequest:
		message = NewWorkflowSleepRequest()

	// WorkflowExecuteChild
	case internal.WorkflowExecuteChildReply:
		message = NewWorkflowExecuteChildReply()
	case internal.WorkflowExecuteChildRequest:
		message = NewWorkflowExecuteChildRequest()

	// WorkflowWaitForChild
	case internal.WorkflowWaitForChildReply:
		message = NewWorkflowWaitForChildReply()
	case internal.WorkflowWaitForChildRequest:
		message = NewWorkflowWaitForChildRequest()

	// WorkflowSignalChild
	case internal.WorkflowSignalChildReply:
		message = NewWorkflowSignalChildReply()
	case internal.WorkflowSignalChildRequest:
		message = NewWorkflowSignalChildRequest()

	// WorkflowCancelChild
	case internal.WorkflowCancelChildReply:
		message = NewWorkflowCancelChildReply()
	case internal.WorkflowCancelChildRequest:
		message = NewWorkflowCancelChildRequest()

	// WorkflowSetQueryHandler
	case internal.WorkflowSetQueryHandlerReply:
		message = NewWorkflowSetQueryHandlerReply()
	case internal.WorkflowSetQueryHandlerRequest:
		message = NewWorkflowSetQueryHandlerRequest()

	// WorkflowQueryInvoke
	case internal.WorkflowQueryInvokeReply:
		message = NewWorkflowQueryInvokeReply()
	case internal.WorkflowQueryInvokeRequest:
		message = NewWorkflowQueryInvokeRequest()

	// WorkflowGetVersion
	case internal.WorkflowGetVersionReply:
		message = NewWorkflowGetVersionReply()
	case internal.WorkflowGetVersionRequest:
		message = NewWorkflowGetVersionRequest()

	// WorkflowFutureReady
	case internal.WorkflowFutureReadyReply:
		message = NewWorkflowFutureReadyReply()
	case internal.WorkflowFutureReadyRequest:
		message = NewWorkflowFutureReadyRequest()

	// WorkflowQueueNew
	case internal.WorkflowQueueNewReply:
		message = NewWorkflowQueueNewReply()
	case internal.WorkflowQueueNewRequest:
		message = NewWorkflowQueueNewRequest()

	// WorkflowQueueWrite
	case internal.WorkflowQueueWriteReply:
		message = NewWorkflowQueueWriteReply()
	case internal.WorkflowQueueWriteRequest:
		message = NewWorkflowQueueWriteRequest()

	// WorkflowQueueRead
	case internal.WorkflowQueueReadReply:
		message = NewWorkflowQueueReadReply()
	case internal.WorkflowQueueReadRequest:
		message = NewWorkflowQueueReadRequest()

	// WorkflowQueueClose
	case internal.WorkflowQueueCloseReply:
		message = NewWorkflowQueueCloseReply()
	case internal.WorkflowQueueCloseRequest:
		message = NewWorkflowQueueCloseRequest()

	// --------------------------------------------------------------------------
	// Activity messages

	// ActivityExecute
	case internal.ActivityExecuteReply:
		message = NewActivityExecuteReply()
	case internal.ActivityExecuteRequest:
		message = NewActivityExecuteRequest()

	// ActivityInvoke
	case internal.ActivityInvokeReply:
		message = NewActivityInvokeReply()
	case internal.ActivityInvokeRequest:
		message = NewActivityInvokeRequest()

	// ActivityGetHeartbeatDetails
	case internal.ActivityGetHeartbeatDetailsReply:
		message = NewActivityGetHeartbeatDetailsReply()
	case internal.ActivityGetHeartbeatDetailsRequest:
		message = NewActivityGetHeartbeatDetailsRequest()

	// ActivityRecordHeartbeat
	case internal.ActivityRecordHeartbeatReply:
		message = NewActivityRecordHeartbeatReply()
	case internal.ActivityRecordHeartbeatRequest:
		message = NewActivityRecordHeartbeatRequest()

	// ActivityHasHeartbeatDetails
	case internal.ActivityHasHeartbeatDetailsReply:
		message = NewActivityHasHeartbeatDetailsReply()
	case internal.ActivityHasHeartbeatDetailsRequest:
		message = NewActivityHasHeartbeatDetailsRequest()

	// ActivityStopping
	case internal.ActivityStoppingReply:
		message = NewActivityStoppingReply()
	case internal.ActivityStoppingRequest:
		message = NewActivityStoppingRequest()

	// ActivityRegister
	case internal.ActivityRegisterReply:
		message = NewActivityRegisterReply()
	case internal.ActivityRegisterRequest:
		message = NewActivityRegisterRequest()

	// ActivityExecuteLocal
	case internal.ActivityExecuteLocalReply:
		message = NewActivityExecuteLocalReply()
	case internal.ActivityExecuteLocalRequest:
		message = NewActivityExecuteLocalRequest()

	// ActivityInvokeLocal
	case internal.ActivityInvokeLocalReply:
		message = NewActivityInvokeLocalReply()
	case internal.ActivityInvokeLocalRequest:
		message = NewActivityInvokeLocalRequest()

	// ActivityGetInfo
	case internal.ActivityGetInfoReply:
		message = NewActivityGetInfoReply()
	case internal.ActivityGetInfoRequest:
		message = NewActivityGetInfoRequest()

	// ActivityComplete
	case internal.ActivityCompleteReply:
		message = NewActivityCompleteReply()
	case internal.ActivityCompleteRequest:
		message = NewActivityCompleteRequest()

	// ActivityStart
	case internal.ActivityStartReply:
		message = NewActivityStartReply()
	case internal.ActivityStartRequest:
		message = NewActivityStartRequest()

	// ActivityGetResult
	case internal.ActivityGetResultReply:
		message = NewActivityGetResultReply()
	case internal.ActivityGetResultRequest:
		message = NewActivityGetResultRequest()

		// ActivityStartLocal
	case internal.ActivityStartLocalReply:
		message = NewActivityStartLocalReply()
	case internal.ActivityStartLocalRequest:
		message = NewActivityStartLocalRequest()

	// ActivityGetLocalResult
	case internal.ActivityGetLocalResultReply:
		message = NewActivityGetLocalResultReply()
	case internal.ActivityGetLocalResultRequest:
		message = NewActivityGetLocalResultRequest()

	// default
	default:
		return nil
	}

	return message
}
