package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowReply is base type for all workflow replies.
	// All workflow replies will inherit from WorkflowReply
	//
	// A WorkflowReply contains a reference to a
	// ProxyReply struct in memory
	WorkflowReply struct {
		*ProxyReply
	}

	// IWorkflowReply is the interface that all workflow message replies
	// implement.
	IWorkflowReply interface {
		IProxyReply
		GetContextID() int64
		SetContextID(value int64)
	}
)

// NewWorkflowReply is the default constructor for WorkflowReply.
// It creates a new WorkflowReply in memory and then creates and sets
// a reference to a new ProxyReply in the WorkflowReply.
//
// returns *WorkflowReply -> a pointer to a new WorkflowReply in memory
func NewWorkflowReply() *WorkflowReply {
	reply := new(WorkflowReply)
	reply.ProxyReply = NewProxyReply()
	reply.SetType(messagetypes.Unspecified)

	return reply
}

// -------------------------------------------------------------------------
// IWorkflowReply interface methods for implementing the IWorkflowReply interface

// GetContextID gets the ContextId from a WorkflowReply's properties
// map.
//
// returns int64 -> the long representing a WorkflowReply's ContextId
func (reply *WorkflowReply) GetContextID() int64 {
	return reply.GetLongProperty("ContextID")
}

// SetContextID sets the ContextId in a WorkflowReply's properties map
//
// param value int64 -> int64 value to set as the WorkflowReply's ContextId
// in its properties map
func (reply *WorkflowReply) SetContextID(value int64) {
	reply.SetLongProperty("ContextID", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyReply.Clone()
func (reply *WorkflowReply) Clone() IProxyMessage {
	workflowContextReply := NewWorkflowReply()
	var messageClone IProxyMessage = workflowContextReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyReply.CopyTo()
func (reply *WorkflowReply) CopyTo(target IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
	if v, ok := target.(IWorkflowReply); ok {
		v.SetContextID(reply.GetContextID())
	}
}
