package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowMutableInvokeReply is a WorkflowReply of MessageType
	// WorkflowMutableInvokeReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowInvokeRequest
	WorkflowMutableInvokeReply struct {
		*WorkflowReply
	}
)

// NewWorkflowMutableInvokeReply is the default constructor for
// a WorkflowMutableInvokeReply
//
// returns *WorkflowMutableInvokeReply -> a pointer to a newly initialized
// WorkflowMutableInvokeReply in memory
func NewWorkflowMutableInvokeReply() *WorkflowMutableInvokeReply {
	reply := new(WorkflowMutableInvokeReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(messagetypes.WorkflowMutableInvokeReply)

	return reply
}

// GetResult gets the workflow execution result or nil
// from a WorkflowMutableInvokeReply's properties map.
//
// returns []byte -> []byte representing the result of a workflow execution
func (reply *WorkflowMutableInvokeReply) GetResult() []byte {
	return reply.GetBytesProperty("Result")
}

// SetResult sets the workflow execution result or nil
// in a WorkflowMutableInvokeReply's properties map.
//
// param value []byte -> []byte representing the result of a workflow execution
// to be set in the WorkflowMutableInvokeReply's properties map
func (reply *WorkflowMutableInvokeReply) SetResult(value []byte) {
	reply.SetBytesProperty("Result", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (reply *WorkflowMutableInvokeReply) Clone() IProxyMessage {
	workflowMutableInvokeReply := NewWorkflowMutableInvokeReply()
	var messageClone IProxyMessage = workflowMutableInvokeReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *WorkflowMutableInvokeReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
	if v, ok := target.(*WorkflowMutableInvokeReply); ok {
		v.SetResult(reply.GetResult())
	}
}
