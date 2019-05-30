package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowListOpenExecutionsReply is a WorkflowReply of MessageType
	// WorkflowListOpenExecutionsReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowListOpenExecutionsRequest
	WorkflowListOpenExecutionsReply struct {
		*WorkflowReply
	}
)

// NewWorkflowListOpenExecutionsReply is the default constructor for
// a WorkflowListOpenExecutionsReply
//
// returns *WorkflowListOpenExecutionsReply -> a pointer to a newly initialized
// WorkflowListOpenExecutionsReply in memory
func NewWorkflowListOpenExecutionsReply() *WorkflowListOpenExecutionsReply {
	reply := new(WorkflowListOpenExecutionsReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(messagetypes.WorkflowListOpenExecutionsReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowListOpenExecutionsReply) Clone() IProxyMessage {
	workflowListOpenExecutionsReply := NewWorkflowListOpenExecutionsReply()
	var messageClone IProxyMessage = workflowListOpenExecutionsReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowListOpenExecutionsReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
}
