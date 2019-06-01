package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

// CreateNewTypedMessage creates newly initialized message of the specified type
//
// param messageType message.MessageType -> the MessageType to be created
//
// returns IProxyMessage -> the initialized message as an IProxyMessage interface
func CreateNewTypedMessage(messageType messagetypes.MessageType) IProxyMessage {
	var message IProxyMessage
	switch messageType {

	// --------------------------------------------------------------------------
	// Client messages

	// Cancel
	case messagetypes.CancelRequest:
		message = NewCancelRequest()
	case messagetypes.CancelReply:
		message = NewCancelReply()

	// Connect
	case messagetypes.ConnectReply:
		message = NewConnectReply()
	case messagetypes.ConnectRequest:
		message = NewConnectRequest()

	// DomainDescribe
	case messagetypes.DomainDescribeReply:
		message = NewDomainDescribeReply()
	case messagetypes.DomainDescribeRequest:
		message = NewDomainDescribeRequest()

	// DomainRegister
	case messagetypes.DomainRegisterReply:
		message = NewDomainRegisterReply()
	case messagetypes.DomainRegisterRequest:
		message = NewDomainRegisterRequest()

	// DomainUpdate
	case messagetypes.DomainUpdateReply:
		message = NewDomainUpdateReply()
	case messagetypes.DomainUpdateRequest:
		message = NewDomainUpdateRequest()

	// Heartbeat
	case messagetypes.HeartbeatReply:
		message = NewHeartbeatReply()
	case messagetypes.HeartbeatRequest:
		message = NewHeartbeatRequest()

	// Initialize
	case messagetypes.InitializeReply:
		message = NewInitializeReply()
	case messagetypes.InitializeRequest:
		message = NewInitializeRequest()

	// Terminate
	case messagetypes.TerminateReply:
		message = NewTerminateReply()
	case messagetypes.TerminateRequest:
		message = NewTerminateRequest()

	// NewWorker
	case messagetypes.NewWorkerReply:
		message = NewNewWorkerReply()
	case messagetypes.NewWorkerRequest:
		message = NewNewWorkerRequest()

	// StopWorker
	case messagetypes.StopWorkerRequest:
		message = NewStopWorkerRequest()
	case messagetypes.StopWorkerReply:
		message = NewStopWorkerReply()

	// Ping
	case messagetypes.PingReply:
		message = NewPingReply()
	case messagetypes.PingRequest:
		message = NewPingRequest()

	// --------------------------------------------------------------------------
	// Workflow messages

	// WorkflowExecute
	case messagetypes.WorkflowExecuteReply:
		message = NewWorkflowExecuteReply()
	case messagetypes.WorkflowExecuteRequest:
		message = NewWorkflowExecuteRequest()

	// WorkflowInvoke
	case messagetypes.WorkflowInvokeReply:
		message = NewWorkflowInvokeReply()
	case messagetypes.WorkflowInvokeRequest:
		message = NewWorkflowInvokeRequest()

	// WorkflowRegister
	case messagetypes.WorkflowRegisterReply:
		message = NewWorkflowRegisterReply()
	case messagetypes.WorkflowRegisterRequest:
		message = NewWorkflowRegisterRequest()

	// WorkflowCancel
	case messagetypes.WorkflowCancelReply:
		message = NewWorkflowCancelReply()
	case messagetypes.WorkflowCancelRequest:
		message = NewWorkflowCancelRequest()

	// WorkflowSignal
	case messagetypes.WorkflowSignalRequest:
		message = NewWorkflowSignalRequest()
	case messagetypes.WorkflowSignalReply:
		message = NewWorkflowSignalReply()

	// WorkflowTerminate
	case messagetypes.WorkflowTerminateReply:
		message = NewWorkflowTerminateReply()
	case messagetypes.WorkflowTerminateRequest:
		message = NewWorkflowTerminateRequest()

	// WorkflowSignalWithStart
	case messagetypes.WorkflowSignalWithStartReply:
		message = NewWorkflowSignalWithStartReply()
	case messagetypes.WorkflowSignalWithStartRequest:
		message = NewWorkflowSignalWithStartRequest()

	// WorkflowSetCacheSize
	case messagetypes.WorkflowSetCacheSizeRequest:
		message = NewWorkflowSetCacheSizeRequest()
	case messagetypes.WorkflowSetCacheSizeReply:
		message = NewWorkflowSetCacheSizeReply()

	// WorkflowQuery
	case messagetypes.WorkflowQueryReply:
		message = NewWorkflowQueryReply()
	case messagetypes.WorkflowQueryRequest:
		message = NewWorkflowQueryRequest()

	// WorkflowMutableInvoke
	case messagetypes.WorkflowMutableInvokeReply:
		message = NewWorkflowMutableInvokeReply()
	case messagetypes.WorkflowMutableInvokeRequest:
		message = NewWorkflowMutableInvokeRequest()

	// WorkflowMutable
	case messagetypes.WorkflowMutableReply:
		message = NewWorkflowMutableReply()
	case messagetypes.WorkflowMutableRequest:
		message = NewWorkflowMutableRequest()

	// WorkflowDescribeExecution
	case messagetypes.WorkflowDescribeExecutionReply:
		message = NewWorkflowDescribeExecutionReply()
	case messagetypes.WorkflowDescribeExecutionRequest:
		message = NewWorkflowDescribeExecutionRequest()

	// WorkflowGetResult
	case messagetypes.WorkflowGetResultRequest:
		message = NewWorkflowGetResultRequest()
	case messagetypes.WorkflowGetResultReply:
		message = NewWorkflowGetResultReply()

	// WorkflowListOpenExecutions
	case messagetypes.WorkflowListOpenExecutionsReply:
		message = NewWorkflowListOpenExecutionsReply()
	case messagetypes.WorkflowListOpenExecutionsRequest:
		message = NewWorkflowListOpenExecutionsRequest()

	// WorkflowListClosed
	case messagetypes.WorkflowListClosedReply:
		message = NewWorkflowListClosedReply()
	case messagetypes.WorkflowListClosedRequest:
		message = NewWorkflowListClosedRequest()

	// WorkflowDescribeTaskList
	case messagetypes.WorkflowDescribeTaskListReply:
		message = NewWorkflowDescribeTaskListReply()
	case messagetypes.WorkflowDescribeTaskListRequest:
		message = NewWorkflowDescribeTaskListRequest()

	// WorkflowSignalSubscribe
	case messagetypes.WorkflowSignalSubscribeReply:
		message = NewWorkflowSignalSubscribeReply()
	case messagetypes.WorkflowSignalSubscribeRequest:
		message = NewWorkflowSignalSubscribeRequest()

	// WorkflowSignalReceived
	case messagetypes.WorkflowSignalReceivedReply:
		message = NewWorkflowSignalReceivedReply()
	case messagetypes.WorkflowSignalReceivedRequest:
		message = NewWorkflowSignalReceivedRequest()

	// WorkflowHasLastResult
	case messagetypes.WorkflowHasLastResultReply:
		message = NewWorkflowHasLastResultReply()
	case messagetypes.WorkflowHasLastResultRequest:
		message = NewWorkflowHasLastResultRequest()

	// WorkflowGetLastResult
	case messagetypes.WorkflowGetLastResultReply:
		message = NewWorkflowGetLastResultReply()
	case messagetypes.WorkflowGetLastResultRequest:
		message = NewWorkflowGetLastResultRequest()

	// WorkflowDisconnectContext
	case messagetypes.WorkflowDisconnectContextReply:
		message = NewWorkflowDisconnectContextReply()
	case messagetypes.WorkflowDisconnectContextRequest:
		message = NewWorkflowDisconnectContextRequest()

	// WorkflowGetTime
	case messagetypes.WorkflowGetTimeReply:
		message = NewWorkflowGetTimeReply()
	case messagetypes.WorkflowGetTimeRequest:
		message = NewWorkflowGetTimeRequest()

	// WorkflowSleep
	case messagetypes.WorkflowSleepReply:
		message = NewWorkflowSleepReply()
	case messagetypes.WorkflowSleepRequest:
		message = NewWorkflowSleepRequest()

	// WorkflowExecuteChild
	case messagetypes.WorkflowExecuteChildReply:
		message = NewWorkflowExecuteChildReply()
	case messagetypes.WorkflowExecuteChildRequest:
		message = NewWorkflowExecuteChildRequest()

	// WorkflowWaitForChild
	case messagetypes.WorkflowWaitForChildReply:
		message = NewWorkflowWaitForChildReply()
	case messagetypes.WorkflowWaitForChildRequest:
		message = NewWorkflowWaitForChildRequest()

	// WorkflowSignalChild
	case messagetypes.WorkflowSignalChildReply:
		message = NewWorkflowSignalChildReply()
	case messagetypes.WorkflowSignalChildRequest:
		message = NewWorkflowSignalChildRequest()

	// WorkflowCancelChild
	case messagetypes.WorkflowCancelChildReply:
		message = NewWorkflowCancelChildReply()
	case messagetypes.WorkflowCancelChildRequest:
		message = NewWorkflowCancelChildRequest()

	// WorkflowSetSignalHandler
	case messagetypes.WorkflowSetSignalHandlerReply:
		message = NewWorkflowSetSignalHandlerReply()
	case messagetypes.WorkflowSetSignalHandlerRequest:
		message = NewWorkflowSetSignalHandlerRequest()

	// WorkflowSetQueryHandler
	case messagetypes.WorkflowSetQueryHandlerReply:
		message = NewWorkflowSetQueryHandlerReply()
	case messagetypes.WorkflowSetQueryHandlerRequest:
		message = NewWorkflowSetQueryHandlerRequest()

	// --------------------------------------------------------------------------
	// Activity messages

	// ActivityExecute
	case messagetypes.ActivityExecuteReply:
		message = NewActivityExecuteReply()
	case messagetypes.ActivityExecuteRequest:
		message = NewActivityExecuteRequest()

	// ActivityInvoke
	case messagetypes.ActivityInvokeReply:
		message = NewActivityInvokeReply()
	case messagetypes.ActivityInvokeRequest:
		message = NewActivityInvokeRequest()

	// ActivityGetHeartbeatDetails
	case messagetypes.ActivityGetHeartbeatDetailsReply:
		message = NewActivityGetHeartbeatDetailsReply()
	case messagetypes.ActivityGetHeartbeatDetailsRequest:
		message = NewActivityGetHeartbeatDetailsRequest()

	// ActivityRecordHeartbeat
	case messagetypes.ActivityRecordHeartbeatReply:
		message = NewActivityRecordHeartbeatReply()
	case messagetypes.ActivityRecordHeartbeatRequest:
		message = NewActivityRecordHeartbeatRequest()

	// ActivityHasHeartbeatDetails
	case messagetypes.ActivityHasHeartbeatDetailsReply:
		message = NewActivityHasHeartbeatDetailsReply()
	case messagetypes.ActivityHasHeartbeatDetailsRequest:
		message = NewActivityHasHeartbeatDetailsRequest()

	// ActivityStopping
	case messagetypes.ActivityStoppingReply:
		message = NewActivityStoppingReply()
	case messagetypes.ActivityStoppingRequest:
		message = NewActivityStoppingRequest()

	// ActivityRegister
	case messagetypes.ActivityRegisterReply:
		message = NewActivityRegisterReply()
	case messagetypes.ActivityRegisterRequest:
		message = NewActivityRegisterRequest()

	// ActivityExecuteLocal
	case messagetypes.ActivityExecuteLocalReply:
		message = NewActivityExecuteLocalReply()
	case messagetypes.ActivityExecuteLocalRequest:
		message = NewActivityExecuteLocalRequest()

	// ActivityInvokeLocal
	case messagetypes.ActivityInvokeLocalReply:
		message = NewActivityInvokeLocalReply()
	case messagetypes.ActivityInvokeLocalRequest:
		message = NewActivityInvokeLocalRequest()

	// default
	default:
		return nil
	}

	return message
}
