package messages

import (
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowContextReply is base type for all workflow replies.
	// All workflow replies will inherit from WorkflowContextReply
	//
	// A WorkflowContextReply contains a reference to a
	// ProxyReply struct in memory
	WorkflowContextReply struct {
		*ProxyReply
	}

	// IWorkflowContextReply is the interface that all workflow message replies
	// implement.
	IWorkflowContextReply interface {
		GetWorkflowContextID() int64
		SetWorkflowContextID(value int64)
	}
)

// NewWorkflowContextReply is the default constructor for WorkflowContextReply.
// It creates a new WorkflowContextReply in memory and then creates and sets
// a reference to a new ProxyReply in the WorkflowContextReply.
//
// returns *WorkflowContextReply -> a pointer to a new WorkflowContextReply in memory
func NewWorkflowContextReply() *WorkflowContextReply {
	reply := new(WorkflowContextReply)
	reply.ProxyReply = NewProxyReply()
	reply.Type = messagetypes.Unspecified

	return reply
}

// -------------------------------------------------------------------------
// IWorkflowContextReply interface methods for implementing the IWorkflowContextReply interface

// GetWorkflowContextID gets the ContextId from a WorkflowContextReply's properties
// map.
//
// returns int64 -> the long representing a WorkflowContextReply's ContextId
func (reply *WorkflowContextReply) GetWorkflowContextID() int64 {
	return reply.GetLongProperty("WorkflowContextId")
}

// SetWorkflowContextID sets the ContextId in a WorkflowContextReply's properties map
//
// param value int64 -> int64 value to set as the WorkflowContextReply's ContextId
// in its properties map
func (reply *WorkflowContextReply) SetWorkflowContextID(value int64) {
	reply.SetLongProperty("WorkflowContextId", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyReply.Clone()
func (reply *WorkflowContextReply) Clone() IProxyMessage {
	workflowContextReply := NewWorkflowContextReply()
	var messageClone IProxyMessage = workflowContextReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyReply.CopyTo()
func (reply *WorkflowContextReply) CopyTo(target IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
	if v, ok := target.(IWorkflowContextReply); ok {
		v.SetWorkflowContextID(reply.GetWorkflowContextID())
	}
}

// SetProxyMessage inherits docs from ProxyReply.SetProxyMessage()
func (reply *WorkflowContextReply) SetProxyMessage(value *ProxyMessage) {
	reply.ProxyReply.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ProxyReply.GetProxyMessage()
func (reply *WorkflowContextReply) GetProxyMessage() *ProxyMessage {
	return reply.ProxyReply.GetProxyMessage()
}

// GetRequestID inherits docs from ProxyReply.GetRequestID()
func (reply *WorkflowContextReply) GetRequestID() int64 {
	return reply.ProxyReply.GetRequestID()
}

// SetRequestID inherits docs from ProxyReply.SetRequestID()
func (reply *WorkflowContextReply) SetRequestID(value int64) {
	reply.ProxyReply.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from IProxyReply.GetError()
func (reply *WorkflowContextReply) GetError() *cadenceerrors.CadenceError {
	return reply.ProxyReply.GetError()
}

// SetError inherits docs from IProxyReply.SetError()
func (reply *WorkflowContextReply) SetError(value *cadenceerrors.CadenceError) {
	reply.ProxyReply.SetError(value)
}
