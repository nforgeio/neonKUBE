package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowListClosedReply is a WorkflowReply of MessageType
	// WorkflowListClosedReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowListClosedRequest
	WorkflowListClosedReply struct {
		*WorkflowReply
	}
)

// NewWorkflowListClosedReply is the default constructor for
// a WorkflowListClosedReply
//
// returns *WorkflowListClosedReply -> a pointer to a newly initialized
// WorkflowListClosedReply in memory
func NewWorkflowListClosedReply() *WorkflowListClosedReply {
	reply := new(WorkflowListClosedReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(messagetypes.WorkflowListClosedReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowListClosedReply) Clone() IProxyMessage {
	WorkflowListClosedReply := NewWorkflowListClosedReply()
	var messageClone IProxyMessage = WorkflowListClosedReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowListClosedReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
}
