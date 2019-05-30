package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowSetQueryHandlerReply is a WorkflowReply of MessageType
	// WorkflowSetQueryHandlerReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowSetQueryHandlerRequest
	WorkflowSetQueryHandlerReply struct {
		*WorkflowReply
	}
)

// NewWorkflowSetQueryHandlerReply is the default constructor for
// a WorkflowSetQueryHandlerReply
//
// returns *WorkflowSetQueryHandlerReply -> a pointer to a newly initialized
// WorkflowSetQueryHandlerReply in memory
func NewWorkflowSetQueryHandlerReply() *WorkflowSetQueryHandlerReply {
	reply := new(WorkflowSetQueryHandlerReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(messagetypes.WorkflowSetQueryHandlerReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowSetQueryHandlerReply) Clone() IProxyMessage {
	workflowSetQueryHandlerReply := NewWorkflowSetQueryHandlerReply()
	var messageClone IProxyMessage = workflowSetQueryHandlerReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowSetQueryHandlerReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
}
