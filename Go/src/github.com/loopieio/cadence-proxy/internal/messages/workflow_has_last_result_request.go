package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowHasLastResultRequest is WorkflowRequest of MessageType
	// WorkflowHasLastResultRequest.
	//
	// A WorkflowHasLastResultRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// A WorkflowHasLastResultRequest determines whether the last execution of the workflow has
	// a completion result.  This can be used by CRON workflows to determine whether the
	// last run returned a result.
	WorkflowHasLastResultRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowHasLastResultRequest is the default constructor for a WorkflowHasLastResultRequest
//
// returns *WorkflowHasLastResultRequest -> a reference to a newly initialized
// WorkflowHasLastResultRequest in memory
func NewWorkflowHasLastResultRequest() *WorkflowHasLastResultRequest {
	request := new(WorkflowHasLastResultRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(messagetypes.WorkflowHasLastResultRequest)
	request.SetReplyType(messagetypes.WorkflowHasLastResultReply)

	return request
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowHasLastResultRequest) Clone() IProxyMessage {
	workflowHasLastResultRequest := NewWorkflowHasLastResultRequest()
	var messageClone IProxyMessage = workflowHasLastResultRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowHasLastResultRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
}
