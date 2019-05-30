package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowCancelReply is a WorkflowReply of MessageType
	// WorkflowCancelReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowCancelRequest
	WorkflowCancelReply struct {
		*WorkflowReply
	}
)

// NewWorkflowCancelReply is the default constructor for
// a WorkflowCancelReply
//
// returns *WorkflowCancelReply -> a pointer to a newly initialized
// WorkflowCancelReply in memory
func NewWorkflowCancelReply() *WorkflowCancelReply {
	reply := new(WorkflowCancelReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(messagetypes.WorkflowCancelReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowCancelReply) Clone() IProxyMessage {
	workflowCancelReply := NewWorkflowCancelReply()
	var messageClone IProxyMessage = workflowCancelReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowCancelReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
}
