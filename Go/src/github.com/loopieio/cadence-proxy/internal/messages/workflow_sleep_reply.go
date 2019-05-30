package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowSleepReply is a WorkflowReply of MessageType
	// WorkflowSleepReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowSleepRequest
	WorkflowSleepReply struct {
		*WorkflowReply
	}
)

// NewWorkflowSleepReply is the default constructor for
// a WorkflowSleepReply
//
// returns *WorkflowSleepReply -> a pointer to a newly initialized
// WorkflowSleepReply in memory
func NewWorkflowSleepReply() *WorkflowSleepReply {
	reply := new(WorkflowSleepReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(messagetypes.WorkflowSleepReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowSleepReply) Clone() IProxyMessage {
	workflowSleepReply := NewWorkflowSleepReply()
	var messageClone IProxyMessage = workflowSleepReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowSleepReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
}
