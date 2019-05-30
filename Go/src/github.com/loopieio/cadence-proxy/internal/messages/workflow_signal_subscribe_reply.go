package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowSignalSubscribeReply is a WorkflowReply of MessageType
	// WorkflowSignalSubscribeReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowSignalSubscribeRequest
	WorkflowSignalSubscribeReply struct {
		*WorkflowReply
	}
)

// NewWorkflowSignalSubscribeReply is the default constructor for
// a WorkflowSignalSubscribeReply
//
// returns *WorkflowSignalSubscribeReply -> a pointer to a newly initialized
// WorkflowSignalSubscribeReply in memory
func NewWorkflowSignalSubscribeReply() *WorkflowSignalSubscribeReply {
	reply := new(WorkflowSignalSubscribeReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(messagetypes.WorkflowSignalSubscribeReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowSignalSubscribeReply) Clone() IProxyMessage {
	workflowSignalSubscribeReply := NewWorkflowSignalSubscribeReply()
	var messageClone IProxyMessage = workflowSignalSubscribeReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowSignalSubscribeReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
}
