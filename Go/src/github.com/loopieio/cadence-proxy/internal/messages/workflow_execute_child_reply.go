package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowExecuteChildReply is a WorkflowReply of MessageType
	// WorkflowExecuteChildReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowInvokeRequest
	WorkflowExecuteChildReply struct {
		*WorkflowReply
	}
)

// NewWorkflowExecuteChildReply is the default constructor for
// a WorkflowExecuteChildReply
//
// returns *WorkflowExecuteChildReply -> a pointer to a newly initialized
// WorkflowExecuteChildReply in memory
func NewWorkflowExecuteChildReply() *WorkflowExecuteChildReply {
	reply := new(WorkflowExecuteChildReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(messagetypes.WorkflowExecuteChildReply)

	return reply
}

// GetChildID gets a WorkflowExecuteChildReply's ChildID value
// from its properties map. Identifies the child workflow.
//
// returns int64 -> long holding the value
// of a WorkflowExecuteChildReply's ChildID
func (reply *WorkflowExecuteChildReply) GetChildID() int64 {
	return reply.GetLongProperty("ChildId")
}

// SetChildID sets an WorkflowExecuteChildReply's ChildID value
// in its properties map. Identifies the child workflow.
//
// param value int64 -> long holding the value
// of a WorkflowExecuteChildReply's ChildID to be set in the
// WorkflowExecuteChildReply's properties map.
func (reply *WorkflowExecuteChildReply) SetChildID(value int64) {
	reply.SetLongProperty("ChildId", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (reply *WorkflowExecuteChildReply) Clone() IProxyMessage {
	workflowExecuteChildReply := NewWorkflowExecuteChildReply()
	var messageClone IProxyMessage = workflowExecuteChildReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *WorkflowExecuteChildReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
	if v, ok := target.(*WorkflowExecuteChildReply); ok {
		v.SetChildID(reply.GetChildID())
	}
}
