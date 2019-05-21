package messages

import (
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowSetCacheSizeReply is a WorkflowContextReply of MessageType
	// WorkflowSetCacheSizeReply.  It holds a reference to a WorkflowContextReply in memory
	// and is the reply type to a WorkflowSetCacheSizeRequest
	WorkflowSetCacheSizeReply struct {
		*WorkflowContextReply
	}
)

// NewWorkflowSetCacheSizeReply is the default constructor for
// a WorkflowSetCacheSizeReply
//
// returns *WorkflowSetCacheSizeReply -> a pointer to a newly initialized
// WorkflowSetCacheSizeReply in memory
func NewWorkflowSetCacheSizeReply() *WorkflowSetCacheSizeReply {
	reply := new(WorkflowSetCacheSizeReply)
	reply.WorkflowContextReply = NewWorkflowContextReply()
	reply.Type = messagetypes.WorkflowSetCacheSizeReply

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowContextReply.Clone()
func (reply *WorkflowSetCacheSizeReply) Clone() IProxyMessage {
	workflowSetCacheSizeReply := NewWorkflowSetCacheSizeReply()
	var messageClone IProxyMessage = workflowSetCacheSizeReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowContextReply.CopyTo()
func (reply *WorkflowSetCacheSizeReply) CopyTo(target IProxyMessage) {
	reply.WorkflowContextReply.CopyTo(target)
}

// SetProxyMessage inherits docs from WorkflowContextReply.SetProxyMessage()
func (reply *WorkflowSetCacheSizeReply) SetProxyMessage(value *ProxyMessage) {
	reply.WorkflowContextReply.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from WorkflowContextReply.GetProxyMessage()
func (reply *WorkflowSetCacheSizeReply) GetProxyMessage() *ProxyMessage {
	return reply.WorkflowContextReply.GetProxyMessage()
}

// GetRequestID inherits docs from WorkflowContextReply.GetRequestID()
func (reply *WorkflowSetCacheSizeReply) GetRequestID() int64 {
	return reply.WorkflowContextReply.GetRequestID()
}

// SetRequestID inherits docs from WorkflowContextReply.SetRequestID()
func (reply *WorkflowSetCacheSizeReply) SetRequestID(value int64) {
	reply.WorkflowContextReply.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from WorkflowContextReply.GetError()
func (reply *WorkflowSetCacheSizeReply) GetError() *cadenceerrors.CadenceError {
	return reply.WorkflowContextReply.GetError()
}

// SetError inherits docs from WorkflowContextReply.SetError()
func (reply *WorkflowSetCacheSizeReply) SetError(value *cadenceerrors.CadenceError) {
	reply.WorkflowContextReply.SetError(value)
}

// -------------------------------------------------------------------------
// IWorkflowContextReply interface methods for implementing the IWorkflowContextReply interface

// GetWorkflowContextID inherits docs from WorkflowContextReply.GetWorkflowContextID()
func (reply *WorkflowSetCacheSizeReply) GetWorkflowContextID() int64 {
	return reply.WorkflowContextReply.GetWorkflowContextID()
}

// SetWorkflowContextID inherits docs from WorkflowContextReply.GetWorkflowContextID()
func (reply *WorkflowSetCacheSizeReply) SetWorkflowContextID(value int64) {
	reply.WorkflowContextReply.SetWorkflowContextID(value)
}
