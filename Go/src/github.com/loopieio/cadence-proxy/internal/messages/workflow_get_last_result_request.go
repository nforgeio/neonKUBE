package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowGetLastResultRequest is WorkflowRequest of MessageType
	// WorkflowGetLastResultRequest.
	//
	// A WorkflowGetLastResultRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// A WorkflowGetLastResultRequest returns the result from the last execution of the workflow.
	///  This can be used by CRON workflows to retrieve state from the last workflow run.
	WorkflowGetLastResultRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowGetLastResultRequest is the default constructor for a WorkflowGetLastResultRequest
//
// returns *WorkflowGetLastResultRequest -> a reference to a newly initialized
// WorkflowGetLastResultRequest in memory
func NewWorkflowGetLastResultRequest() *WorkflowGetLastResultRequest {
	request := new(WorkflowGetLastResultRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(messagetypes.WorkflowGetLastResultRequest)
	request.SetReplyType(messagetypes.WorkflowGetLastResultReply)

	return request
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowGetLastResultRequest) Clone() IProxyMessage {
	workflowGetLastResultRequest := NewWorkflowGetLastResultRequest()
	var messageClone IProxyMessage = workflowGetLastResultRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowGetLastResultRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
}
