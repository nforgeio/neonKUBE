package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowDisconnectContextReply is a WorkflowReply of MessageType
	// WorkflowDisconnectContextReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowDisconnectContextRequest
	WorkflowDisconnectContextReply struct {
		*WorkflowReply
	}
)

// NewWorkflowDisconnectContextReply is the default constructor for
// a WorkflowDisconnectContextReply
//
// returns *WorkflowDisconnectContextReply -> a pointer to a newly initialized
// WorkflowDisconnectContextReply in memory
func NewWorkflowDisconnectContextReply() *WorkflowDisconnectContextReply {
	reply := new(WorkflowDisconnectContextReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(messagetypes.WorkflowDisconnectContextReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowDisconnectContextReply) Clone() IProxyMessage {
	workflowDisconnectContextReply := NewWorkflowDisconnectContextReply()
	var messageClone IProxyMessage = workflowDisconnectContextReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowDisconnectContextReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
}
