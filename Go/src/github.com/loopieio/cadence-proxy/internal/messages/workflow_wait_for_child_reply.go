package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowWaitForChildReply is a WorkflowReply of MessageType
	// WorkflowWaitForChildReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowWaitForChildRequest
	WorkflowWaitForChildReply struct {
		*WorkflowReply
	}
)

// NewWorkflowWaitForChildReply is the default constructor for
// a WorkflowWaitForChildReply
//
// returns *WorkflowWaitForChildReply -> a pointer to a newly initialized
// WorkflowWaitForChildReply in memory
func NewWorkflowWaitForChildReply() *WorkflowWaitForChildReply {
	reply := new(WorkflowWaitForChildReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(messagetypes.WorkflowWaitForChildReply)

	return reply
}

// GetResult gets the child workflow results encoded as bytes
// from a WorkflowWaitForChildReply's properties map.
//
// returns []byte -> []byte representing the result of a child workflow
func (reply *WorkflowWaitForChildReply) GetResult() []byte {
	return reply.GetBytesProperty("Result")
}

// SetResult sets the child workflow results encoded as bytes
// in a WorkflowWaitForChildReply's properties map.
//
// param value []byte -> []byte representing the result of a child workflow
// to be set in the WorkflowWaitForChildReply's properties map
func (reply *WorkflowWaitForChildReply) SetResult(value []byte) {
	reply.SetBytesProperty("Result", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowWaitForChildReply) Clone() IProxyMessage {
	workflowWaitForChildReply := NewWorkflowWaitForChildReply()
	var messageClone IProxyMessage = workflowWaitForChildReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowWaitForChildReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
	if v, ok := target.(*WorkflowWaitForChildReply); ok {
		v.SetResult(reply.GetResult())
	}
}
