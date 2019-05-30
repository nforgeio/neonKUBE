package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowCancelChildRequest is WorkflowRequest of MessageType
	// WorkflowCancelChildRequest.
	//
	// A WorkflowCancelChildRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// Cancels a child workflow.
	WorkflowCancelChildRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowCancelChildRequest is the default constructor for a WorkflowCancelChildRequest
//
// returns *WorkflowCancelChildRequest -> a reference to a newly initialized
// WorkflowCancelChildRequest in memory
func NewWorkflowCancelChildRequest() *WorkflowCancelChildRequest {
	request := new(WorkflowCancelChildRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(messagetypes.WorkflowCancelChildRequest)
	request.SetReplyType(messagetypes.WorkflowCancelChildReply)

	return request
}

// GetChildID gets a WorkflowCancelChildRequest's ChildID value
// from its properties map. Identifies the child workflow.
//
// returns int64 -> long holding the value
// of a WorkflowCancelChildRequest's ChildID
func (request *WorkflowCancelChildRequest) GetChildID() int64 {
	return request.GetLongProperty("ChildId")
}

// SetChildID sets an WorkflowCancelChildRequest's ChildID value
// in its properties map. Identifies the child workflow.
//
// param value int64 -> long holding the value
// of a WorkflowCancelChildRequest's ChildID to be set in the
// WorkflowCancelChildRequest's properties map.
func (request *WorkflowCancelChildRequest) SetChildID(value int64) {
	request.SetLongProperty("ChildId", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowCancelChildRequest) Clone() IProxyMessage {
	workflowCancelChildRequest := NewWorkflowCancelChildRequest()
	var messageClone IProxyMessage = workflowCancelChildRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowCancelChildRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowCancelChildRequest); ok {
		v.SetChildID(request.GetChildID())
	}
}
