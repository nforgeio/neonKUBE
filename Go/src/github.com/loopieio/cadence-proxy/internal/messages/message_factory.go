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
	case messagetypes.CancelRequest:
		message = NewCancelRequest()
	case messagetypes.CancelReply:
		message = NewCancelReply()
	case messagetypes.ConnectReply:
		message = NewConnectReply()
	case messagetypes.ConnectRequest:
		message = NewConnectRequest()
	case messagetypes.DomainDescribeReply:
		message = NewDomainDescribeReply()
	case messagetypes.DomainDescribeRequest:
		message = NewDomainDescribeRequest()
	case messagetypes.DomainRegisterReply:
		message = NewDomainRegisterReply()
	case messagetypes.DomainRegisterRequest:
		message = NewDomainRegisterRequest()
	case messagetypes.DomainUpdateReply:
		message = NewDomainUpdateReply()
	case messagetypes.DomainUpdateRequest:
		message = NewDomainUpdateRequest()
	case messagetypes.HeartbeatReply:
		message = NewHeartbeatReply()
	case messagetypes.HeartbeatRequest:
		message = NewHeartbeatRequest()
	case messagetypes.InitializeReply:
		message = NewInitializeReply()
	case messagetypes.InitializeRequest:
		message = NewInitializeRequest()
	case messagetypes.TerminateReply:
		message = NewTerminateReply()
	case messagetypes.TerminateRequest:
		message = NewTerminateRequest()
	case messagetypes.WorkflowExecuteReply:
		message = NewWorkflowExecuteReply()
	case messagetypes.WorkflowExecuteRequest:
		message = NewWorkflowExecuteRequest()
	case messagetypes.WorkflowInvokeReply:
		message = NewWorkflowInvokeReply()
	case messagetypes.WorkflowInvokeRequest:
		message = NewWorkflowInvokeRequest()
	case messagetypes.WorkflowRegisterReply:
		message = NewWorkflowRegisterReply()
	case messagetypes.WorkflowRegisterRequest:
		message = NewWorkflowRegisterRequest()
	case messagetypes.NewWorkerReply:
		message = NewNewWorkerReply()
	case messagetypes.NewWorkerRequest:
		message = NewNewWorkerRequest()
	case messagetypes.StopWorkerRequest:
		message = NewStopWorkerRequest()
	case messagetypes.StopWorkerReply:
		message = NewStopWorkerReply()
	case messagetypes.PingReply:
		message = NewPingReply()
	case messagetypes.PingRequest:
		message = NewPingRequest()
	case messagetypes.WorkflowCancelReply:
		message = NewWorkflowCancelReply()
	case messagetypes.WorkflowCancelRequest:
		message = NewWorkflowCancelRequest()
	case messagetypes.WorkflowSignalRequest:
		message = NewWorkflowSignalRequest()
	case messagetypes.WorkflowSignalReply:
		message = NewWorkflowSignalReply()
	case messagetypes.WorkflowTerminateReply:
		message = NewWorkflowTerminateReply()
	case messagetypes.WorkflowTerminateRequest:
		message = NewWorkflowTerminateRequest()
	case messagetypes.WorkflowSignalWithStartReply:
		message = NewWorkflowSignalWithStartReply()
	case messagetypes.WorkflowSignalWithStartRequest:
		message = NewWorkflowSignalWithStartRequest()
	case messagetypes.WorkflowSetCacheSizeRequest:
		message = NewWorkflowSetCacheSizeRequest()
	case messagetypes.WorkflowSetCacheSizeReply:
		message = NewWorkflowSetCacheSizeReply()
	case messagetypes.WorkflowQueryReply:
		message = NewWorkflowQueryReply()
	case messagetypes.WorkflowQueryRequest:
		message = NewWorkflowQueryRequest()
	case messagetypes.WorkflowMutableInvokeReply:
		message = NewWorkflowMutableInvokeReply()
	case messagetypes.WorkflowMutableInvokeRequest:
		message = NewWorkflowMutableInvokeRequest()
	case messagetypes.WorkflowMutableReply:
		message = NewWorkflowMutableReply()
	case messagetypes.WorkflowMutableRequest:
		message = NewWorkflowMutableRequest()
	case messagetypes.WorkflowDescribeExecutionReply:
		message = NewWorkflowDescribeExecutionReply()
	case messagetypes.WorkflowDescribeExecutionRequest:
		message = NewWorkflowDescribeExecutionRequest()
	case messagetypes.WorkflowGetHistoryReply:
		message = NewWorkflowGetHistoryReply()
	case messagetypes.WorkflowGetHistoryRequest:
		message = NewWorkflowGetHistoryRequest()
	case messagetypes.WorkflowListOpenExecutionsReply:
		message = NewWorkflowListOpenExecutionsReply()
	case messagetypes.WorkflowListOpenExecutionsRequest:
		message = NewWorkflowListOpenExecutionsRequest()
	case messagetypes.WorkflowListClosedExecutionsReply:
		message = NewWorkflowListClosedExecutionsReply()
	case messagetypes.WorkflowListClosedExecutionsRequest:
		message = NewWorkflowListClosedExecutionsRequest()
	case messagetypes.WorkflowCountReply:
		message = NewWorkflowCountReply()
	case messagetypes.WorkflowCountRequest:
		message = NewWorkflowCountRequest()
	case messagetypes.WorkflowDescribeTaskListReply:
		message = NewWorkflowDescribeTaskListReply()
	case messagetypes.WorkflowDescribeTaskListRequest:
		message = NewWorkflowDescribeTaskListRequest()
	default:
		return nil
	}

	return message
}
