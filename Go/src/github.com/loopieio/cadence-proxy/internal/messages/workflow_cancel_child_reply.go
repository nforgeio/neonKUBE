package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowCancelChildReply is a WorkflowReply of MessageType
	// WorkflowCancelChildReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowCancelChildRequest
	WorkflowCancelChildReply struct {
		*WorkflowReply
	}
)

// NewWorkflowCancelChildReply is the default constructor for
// a WorkflowCancelChildReply
//
// returns *WorkflowCancelChildReply -> a pointer to a newly initialized
// WorkflowCancelChildReply in memory
func NewWorkflowCancelChildReply() *WorkflowCancelChildReply {
	reply := new(WorkflowCancelChildReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(messagetypes.WorkflowCancelChildReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowCancelChildReply) Clone() IProxyMessage {
	workflowCancelChildReply := NewWorkflowCancelChildReply()
	var messageClone IProxyMessage = workflowCancelChildReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowCancelChildReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
}
