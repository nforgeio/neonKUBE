package messages

import (
	"fmt"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowListClosedRequest is WorkflowRequest of MessageType
	// WorkflowListClosedRequest.
	//
	// A WorkflowListClosedRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// A WorkflowListClosedRequest will pass all of the given data
	// necessary to list the closed cadence workflow execution instances
	WorkflowListClosedRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowListClosedRequest is the default constructor for a WorkflowListClosedRequest
//
// returns *WorkflowListClosedRequest -> a reference to a newly initialized
// WorkflowListClosedRequest in memory
func NewWorkflowListClosedRequest() *WorkflowListClosedRequest {
	request := new(WorkflowListClosedRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(messagetypes.WorkflowListClosedRequest)
	request.SetReplyType(messagetypes.WorkflowListClosedReply)

	return request
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowListClosedRequest) Clone() IProxyMessage {
	WorkflowListClosedRequest := NewWorkflowListClosedRequest()
	var messageClone IProxyMessage = WorkflowListClosedRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowListClosedRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowListClosedRequest); ok {
		fmt.Printf("TODO: JACK -- IMPLEMENT %v", v)
	}
}
