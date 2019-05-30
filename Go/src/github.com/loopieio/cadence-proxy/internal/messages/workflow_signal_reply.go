package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowSignalReply is a WorkflowReply of MessageType
	// WorkflowSignalReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowSignalRequest
	WorkflowSignalReply struct {
		*WorkflowReply
	}
)

// NewWorkflowSignalReply is the default constructor for
// a WorkflowSignalReply
//
// returns *WorkflowSignalReply -> a pointer to a newly initialized
// WorkflowSignalReply in memory
func NewWorkflowSignalReply() *WorkflowSignalReply {
	reply := new(WorkflowSignalReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(messagetypes.WorkflowSignalReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowSignalReply) Clone() IProxyMessage {
	workflowSignalReply := NewWorkflowSignalReply()
	var messageClone IProxyMessage = workflowSignalReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowSignalReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
}
