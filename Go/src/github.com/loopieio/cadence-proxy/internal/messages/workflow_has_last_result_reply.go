package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowHasLastResultReply is a WorkflowReply of MessageType
	// WorkflowHasLastResultReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowHasLastResultRequest
	WorkflowHasLastResultReply struct {
		*WorkflowReply
	}
)

// NewWorkflowHasLastResultReply is the default constructor for
// a WorkflowHasLastResultReply
//
// returns *WorkflowHasLastResultReply -> a pointer to a newly initialized
// WorkflowHasLastResultReply in memory
func NewWorkflowHasLastResultReply() *WorkflowHasLastResultReply {
	reply := new(WorkflowHasLastResultReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(messagetypes.WorkflowHasLastResultReply)

	return reply
}

// GetHasResult gets the HasResult property
// from a WorkflowHasLastResultReply's properties map.
// Indicates whether the workflow has a last completion result.
//
// returns bool -> HasResult from the WorkflowHasLastResultReply's
// properties map
func (reply *WorkflowHasLastResultReply) GetHasResult() bool {
	return reply.GetBoolProperty("HasResult")
}

// SetHasResult sets gets the HasResult property
// in a WorkflowHasLastResultReply's properties map.
// Indicates whether the workflow has a last completion result.
//
// param value bool -> HasResult from the WorkflowHasLastResultReply's
// properties map to be set in the
// WorkflowHasLastResultReply's properties map
func (reply *WorkflowHasLastResultReply) SetHasResult(value bool) {
	reply.SetBoolProperty("HasResult", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowHasLastResultReply) Clone() IProxyMessage {
	workflowHasLastResultReply := NewWorkflowHasLastResultReply()
	var messageClone IProxyMessage = workflowHasLastResultReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowHasLastResultReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
	if v, ok := target.(*WorkflowHasLastResultReply); ok {
		v.SetHasResult(reply.GetHasResult())
	}
}
