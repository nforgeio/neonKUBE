package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowQueryReply is a WorkflowReply of MessageType
	// WorkflowQueryReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowInvokeRequest
	WorkflowQueryReply struct {
		*WorkflowReply
	}
)

// NewWorkflowQueryReply is the default constructor for
// a WorkflowQueryReply
//
// returns *WorkflowQueryReply -> a pointer to a newly initialized
// WorkflowQueryReply in memory
func NewWorkflowQueryReply() *WorkflowQueryReply {
	reply := new(WorkflowQueryReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(messagetypes.WorkflowQueryReply)

	return reply
}

// GetResult gets the workflow execution result or nil
// from a WorkflowQueryReply's properties map.
//
// returns []byte -> []byte representing the result of a workflow execution
func (reply *WorkflowQueryReply) GetResult() []byte {
	return reply.GetBytesProperty("Result")
}

// SetResult sets the workflow execution result or nil
// in a WorkflowQueryReply's properties map.
//
// param value []byte -> []byte representing the result of a workflow execution
// to be set in the WorkflowQueryReply's properties map
func (reply *WorkflowQueryReply) SetResult(value []byte) {
	reply.SetBytesProperty("Result", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (reply *WorkflowQueryReply) Clone() IProxyMessage {
	workflowQueryReply := NewWorkflowQueryReply()
	var messageClone IProxyMessage = workflowQueryReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *WorkflowQueryReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
	if v, ok := target.(*WorkflowQueryReply); ok {
		v.SetResult(reply.GetResult())
	}
}
