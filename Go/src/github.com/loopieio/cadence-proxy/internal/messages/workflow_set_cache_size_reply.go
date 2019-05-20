package messages

import (
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowSetCacheSizeReply is a ProxyReply of MessageType
	// WorkflowSetCacheSizeReply.  It holds a reference to a ProxyReply in memory
	// and is the reply type to a WorkflowSetCacheSizeRequest
	WorkflowSetCacheSizeReply struct {
		*ProxyReply
	}
)

// NewWorkflowSetCacheSizeReply is the default constructor for
// a WorkflowSetCacheSizeReply
//
// returns *WorkflowSetCacheSizeReply -> a pointer to a newly initialized
// WorkflowSetCacheSizeReply in memory
func NewWorkflowSetCacheSizeReply() *WorkflowSetCacheSizeReply {
	reply := new(WorkflowSetCacheSizeReply)
	reply.ProxyReply = NewProxyReply()
	reply.Type = messagetypes.WorkflowSetCacheSizeReply

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyReply.Clone()
func (reply *WorkflowSetCacheSizeReply) Clone() IProxyMessage {
	workflowSetCacheSizeReply := NewWorkflowSetCacheSizeReply()
	var messageClone IProxyMessage = workflowSetCacheSizeReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyReply.CopyTo()
func (reply *WorkflowSetCacheSizeReply) CopyTo(target IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
}

// SetProxyMessage inherits docs from ProxyReply.SetProxyMessage()
func (reply *WorkflowSetCacheSizeReply) SetProxyMessage(value *ProxyMessage) {
	reply.ProxyReply.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ProxyReply.GetProxyMessage()
func (reply *WorkflowSetCacheSizeReply) GetProxyMessage() *ProxyMessage {
	return reply.ProxyReply.GetProxyMessage()
}

// GetRequestID inherits docs from ProxyReply.GetRequestID()
func (reply *WorkflowSetCacheSizeReply) GetRequestID() int64 {
	return reply.ProxyReply.GetRequestID()
}

// SetRequestID inherits docs from ProxyReply.SetRequestID()
func (reply *WorkflowSetCacheSizeReply) SetRequestID(value int64) {
	reply.ProxyReply.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from ProxyReply.GetError()
func (reply *WorkflowSetCacheSizeReply) GetError() *cadenceerrors.CadenceError {
	return reply.ProxyReply.GetError()
}

// SetError inherits docs from ProxyReply.SetError()
func (reply *WorkflowSetCacheSizeReply) SetError(value *cadenceerrors.CadenceError) {
	reply.ProxyReply.SetError(value)
}
