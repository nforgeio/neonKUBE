package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowRegisterReply is a WorkflowReply of MessageType
	// WorkflowRegisterReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowRegisterRequest
	WorkflowRegisterReply struct {
		*WorkflowReply
	}
)

// NewWorkflowRegisterReply is the default constructor for
// a WorkflowRegisterReply
//
// returns *WorkflowRegisterReply -> a pointer to a newly initialized
// WorkflowRegisterReply in memory
func NewWorkflowRegisterReply() *WorkflowRegisterReply {
	reply := new(WorkflowRegisterReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(messagetypes.WorkflowRegisterReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowRegisterReply) Clone() IProxyMessage {
	workflowRegisterReply := NewWorkflowRegisterReply()
	var messageClone IProxyMessage = workflowRegisterReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowRegisterReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
}
