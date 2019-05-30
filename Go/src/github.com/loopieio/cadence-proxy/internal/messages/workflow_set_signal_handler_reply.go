package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowSetSignalHandlerReply is a WorkflowReply of MessageType
	// WorkflowSetSignalHandlerReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowSetSignalHandlerRequest
	WorkflowSetSignalHandlerReply struct {
		*WorkflowReply
	}
)

// NewWorkflowSetSignalHandlerReply is the default constructor for
// a WorkflowSetSignalHandlerReply
//
// returns *WorkflowSetSignalHandlerReply -> a pointer to a newly initialized
// WorkflowSetSignalHandlerReply in memory
func NewWorkflowSetSignalHandlerReply() *WorkflowSetSignalHandlerReply {
	reply := new(WorkflowSetSignalHandlerReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(messagetypes.WorkflowSetSignalHandlerReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowSetSignalHandlerReply) Clone() IProxyMessage {
	workflowSetSignalHandlerReply := NewWorkflowSetSignalHandlerReply()
	var messageClone IProxyMessage = workflowSetSignalHandlerReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowSetSignalHandlerReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
}
