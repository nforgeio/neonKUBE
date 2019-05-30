package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowGetLastResultReply is a WorkflowReply of MessageType
	// WorkflowGetLastResultReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowInvokeRequest
	WorkflowGetLastResultReply struct {
		*WorkflowReply
	}
)

// NewWorkflowGetLastResultReply is the default constructor for
// a WorkflowGetLastResultReply
//
// returns *WorkflowGetLastResultReply -> a pointer to a newly initialized
// WorkflowGetLastResultReply in memory
func NewWorkflowGetLastResultReply() *WorkflowGetLastResultReply {
	reply := new(WorkflowGetLastResultReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(messagetypes.WorkflowGetLastResultReply)

	return reply
}

// GetResult gets the workflow execution result or nil
// from a WorkflowGetLastResultReply's properties map.
//
// returns []byte -> []byte representing the result of a workflow execution
func (reply *WorkflowGetLastResultReply) GetResult() []byte {
	return reply.GetBytesProperty("Result")
}

// SetResult sets the workflow execution result or nil
// in a WorkflowGetLastResultReply's properties map.
//
// param value []byte -> []byte representing the result of a workflow execution
// to be set in the WorkflowGetLastResultReply's properties map
func (reply *WorkflowGetLastResultReply) SetResult(value []byte) {
	reply.SetBytesProperty("Result", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (reply *WorkflowGetLastResultReply) Clone() IProxyMessage {
	workflowGetLastResultReply := NewWorkflowGetLastResultReply()
	var messageClone IProxyMessage = workflowGetLastResultReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *WorkflowGetLastResultReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
	if v, ok := target.(*WorkflowGetLastResultReply); ok {
		v.SetResult(reply.GetResult())
	}
}
