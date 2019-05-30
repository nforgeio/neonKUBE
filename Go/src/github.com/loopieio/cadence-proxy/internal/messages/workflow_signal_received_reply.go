package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowSignalReceivedReply is a WorkflowReply of MessageType
	// WorkflowSignalReceivedReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowSignalReceivedRequest
	WorkflowSignalReceivedReply struct {
		*WorkflowReply
	}
)

// NewWorkflowSignalReceivedReply is the default constructor for
// a WorkflowSignalReceivedReply
//
// returns *WorkflowSignalReceivedReply -> a pointer to a newly initialized
// WorkflowSignalReceivedReply in memory
func NewWorkflowSignalReceivedReply() *WorkflowSignalReceivedReply {
	reply := new(WorkflowSignalReceivedReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(messagetypes.WorkflowSignalReceivedReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowSignalReceivedReply) Clone() IProxyMessage {
	workflowSignalReceivedReply := NewWorkflowSignalReceivedReply()
	var messageClone IProxyMessage = workflowSignalReceivedReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowSignalReceivedReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
}
