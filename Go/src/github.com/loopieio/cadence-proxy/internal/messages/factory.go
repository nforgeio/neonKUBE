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
	default:
		return nil
	}

	return message
}
