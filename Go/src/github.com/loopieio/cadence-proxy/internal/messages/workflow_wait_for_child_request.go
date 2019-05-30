package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowWaitForChildRequest is WorkflowRequest of MessageType
	// WorkflowWaitForChildRequest.
	//
	// A WorkflowWaitForChildRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// Waits for a child workflow to complete.
	WorkflowWaitForChildRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowWaitForChildRequest is the default constructor for a WorkflowWaitForChildRequest
//
// returns *WorkflowWaitForChildRequest -> a reference to a newly initialized
// WorkflowWaitForChildRequest in memory
func NewWorkflowWaitForChildRequest() *WorkflowWaitForChildRequest {
	request := new(WorkflowWaitForChildRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(messagetypes.WorkflowWaitForChildRequest)
	request.SetReplyType(messagetypes.WorkflowWaitForChildReply)

	return request
}

// GetChildID gets a WorkflowWaitForChildRequest's ChildID value
// from its properties map. Identifies the child workflow.
//
// returns int64 -> long holding the value
// of a WorkflowWaitForChildRequest's ChildID
func (request *WorkflowWaitForChildRequest) GetChildID() int64 {
	return request.GetLongProperty("ChildId")
}

// SetChildID sets an WorkflowWaitForChildRequest's ChildID value
// in its properties map. Identifies the child workflow.
//
// param value int64 -> long holding the value
// of a WorkflowWaitForChildRequest's ChildID to be set in the
// WorkflowWaitForChildRequest's properties map.
func (request *WorkflowWaitForChildRequest) SetChildID(value int64) {
	request.SetLongProperty("ChildId", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowWaitForChildRequest) Clone() IProxyMessage {
	workflowWaitForChildRequest := NewWorkflowWaitForChildRequest()
	var messageClone IProxyMessage = workflowWaitForChildRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowWaitForChildRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowWaitForChildRequest); ok {
		v.SetChildID(request.GetChildID())
	}
}
