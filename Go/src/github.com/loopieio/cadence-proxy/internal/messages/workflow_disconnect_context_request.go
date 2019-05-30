package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowDisconnectContextRequest is WorkflowRequest of MessageType
	// WorkflowDisconnectContextRequest.
	//
	// A WorkflowDisconnectContextRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// Commands cadence-proxy to replace the current workflow
	// context with context that is disconnected from the parent context.
	WorkflowDisconnectContextRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowDisconnectContextRequest is the default constructor for a WorkflowDisconnectContextRequest
//
// returns *WorkflowDisconnectContextRequest -> a reference to a newly initialized
// WorkflowDisconnectContextRequest in memory
func NewWorkflowDisconnectContextRequest() *WorkflowDisconnectContextRequest {
	request := new(WorkflowDisconnectContextRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(messagetypes.WorkflowDisconnectContextRequest)
	request.SetReplyType(messagetypes.WorkflowDisconnectContextReply)

	return request
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowDisconnectContextRequest) Clone() IProxyMessage {
	workflowDisconnectContextRequest := NewWorkflowDisconnectContextRequest()
	var messageClone IProxyMessage = workflowDisconnectContextRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowDisconnectContextRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
}
