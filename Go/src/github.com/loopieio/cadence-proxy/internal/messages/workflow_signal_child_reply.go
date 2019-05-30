package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowSignalChildReply is a WorkflowReply of MessageType
	// WorkflowSignalChildReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowSignalChildRequest
	WorkflowSignalChildReply struct {
		*WorkflowReply
	}
)

// NewWorkflowSignalChildReply is the default constructor for
// a WorkflowSignalChildReply
//
// returns *WorkflowSignalChildReply -> a pointer to a newly initialized
// WorkflowSignalChildReply in memory
func NewWorkflowSignalChildReply() *WorkflowSignalChildReply {
	reply := new(WorkflowSignalChildReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(messagetypes.WorkflowSignalChildReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowSignalChildReply) Clone() IProxyMessage {
	workflowSignalChildReply := NewWorkflowSignalChildReply()
	var messageClone IProxyMessage = workflowSignalChildReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowSignalChildReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
}
