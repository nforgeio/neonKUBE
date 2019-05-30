package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowTerminateReply is a WorkflowReply of MessageType
	// WorkflowTerminateReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowTerminateRequest
	WorkflowTerminateReply struct {
		*WorkflowReply
	}
)

// NewWorkflowTerminateReply is the default constructor for
// a WorkflowTerminateReply
//
// returns *WorkflowTerminateReply -> a pointer to a newly initialized
// WorkflowTerminateReply in memory
func NewWorkflowTerminateReply() *WorkflowTerminateReply {
	reply := new(WorkflowTerminateReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(messagetypes.WorkflowTerminateReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowTerminateReply) Clone() IProxyMessage {
	workflowTerminateReply := NewWorkflowTerminateReply()
	var messageClone IProxyMessage = workflowTerminateReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowTerminateReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
}
