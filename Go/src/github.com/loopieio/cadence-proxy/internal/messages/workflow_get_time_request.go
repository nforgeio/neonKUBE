package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowGetTimeRequest is WorkflowRequest of MessageType
	// WorkflowGetTimeRequest.
	//
	// A WorkflowGetTimeRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// Requests the current workflow time.
	WorkflowGetTimeRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowGetTimeRequest is the default constructor for a WorkflowGetTimeRequest
//
// returns *WorkflowGetTimeRequest -> a reference to a newly initialized
// WorkflowGetTimeRequest in memory
func NewWorkflowGetTimeRequest() *WorkflowGetTimeRequest {
	request := new(WorkflowGetTimeRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(messagetypes.WorkflowGetTimeRequest)
	request.SetReplyType(messagetypes.WorkflowGetTimeReply)

	return request
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowGetTimeRequest) Clone() IProxyMessage {
	workflowGetTimeRequest := NewWorkflowGetTimeRequest()
	var messageClone IProxyMessage = workflowGetTimeRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowGetTimeRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
}
