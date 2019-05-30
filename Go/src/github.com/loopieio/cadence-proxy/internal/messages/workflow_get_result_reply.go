package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowGetResultReply is a WorkflowReply of MessageType
	// WorkflowGetResultReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowGetResultRequest
	WorkflowGetResultReply struct {
		*WorkflowReply
	}
)

// NewWorkflowGetResultReply is the default constructor for
// a WorkflowGetResultReply
//
// returns *WorkflowGetResultReply -> a pointer to a newly initialized
// WorkflowGetResultReply in memory
func NewWorkflowGetResultReply() *WorkflowGetResultReply {
	reply := new(WorkflowGetResultReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(messagetypes.WorkflowGetResultReply)

	return reply
}

// GetResult gets the workflow execution result or nil
// from a WorkflowGetResultReply's properties map.
//
// returns []byte -> []byte representing the result of a workflow execution
func (reply *WorkflowGetResultReply) GetResult() []byte {
	return reply.GetBytesProperty("Result")
}

// SetResult sets the workflow execution result or nil
// in a WorkflowGetResultReply's properties map.
//
// param value []byte -> []byte representing the result of a workflow execution
// to be set in the WorkflowGetResultReply's properties map
func (reply *WorkflowGetResultReply) SetResult(value []byte) {
	reply.SetBytesProperty("Result", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowGetResultReply) Clone() IProxyMessage {
	workflowGetResultReply := NewWorkflowGetResultReply()
	var messageClone IProxyMessage = workflowGetResultReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowGetResultReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
	if v, ok := target.(*WorkflowGetResultReply); ok {
		v.SetResult(reply.GetResult())
	}
}
