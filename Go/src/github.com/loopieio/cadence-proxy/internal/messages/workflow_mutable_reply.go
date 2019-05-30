package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowMutableReply is a WorkflowReply of MessageType
	// WorkflowMutableReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowInvokeRequest
	WorkflowMutableReply struct {
		*WorkflowReply
	}
)

// NewWorkflowMutableReply is the default constructor for
// a WorkflowMutableReply
//
// returns *WorkflowMutableReply -> a pointer to a newly initialized
// WorkflowMutableReply in memory
func NewWorkflowMutableReply() *WorkflowMutableReply {
	reply := new(WorkflowMutableReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(messagetypes.WorkflowMutableReply)

	return reply
}

// GetResult gets the workflow execution result or nil
// from a WorkflowMutableReply's properties map.
//
// returns []byte -> []byte representing the result of a workflow execution
func (reply *WorkflowMutableReply) GetResult() []byte {
	return reply.GetBytesProperty("Result")
}

// SetResult sets the workflow execution result or nil
// in a WorkflowMutableReply's properties map.
//
// param value []byte -> []byte representing the result of a workflow execution
// to be set in the WorkflowMutableReply's properties map
func (reply *WorkflowMutableReply) SetResult(value []byte) {
	reply.SetBytesProperty("Result", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (reply *WorkflowMutableReply) Clone() IProxyMessage {
	workflowMutableReply := NewWorkflowMutableReply()
	var messageClone IProxyMessage = workflowMutableReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *WorkflowMutableReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
	if v, ok := target.(*WorkflowMutableReply); ok {
		v.SetResult(reply.GetResult())
	}
}
