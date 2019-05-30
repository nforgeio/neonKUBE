package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowSetCacheSizeReply is a WorkflowReply of MessageType
	// WorkflowSetCacheSizeReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowSetCacheSizeRequest
	WorkflowSetCacheSizeReply struct {
		*WorkflowReply
	}
)

// NewWorkflowSetCacheSizeReply is the default constructor for
// a WorkflowSetCacheSizeReply
//
// returns *WorkflowSetCacheSizeReply -> a pointer to a newly initialized
// WorkflowSetCacheSizeReply in memory
func NewWorkflowSetCacheSizeReply() *WorkflowSetCacheSizeReply {
	reply := new(WorkflowSetCacheSizeReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(messagetypes.WorkflowSetCacheSizeReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowSetCacheSizeReply) Clone() IProxyMessage {
	workflowSetCacheSizeReply := NewWorkflowSetCacheSizeReply()
	var messageClone IProxyMessage = workflowSetCacheSizeReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowSetCacheSizeReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
}
