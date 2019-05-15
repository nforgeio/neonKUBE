package types

import (
	"github.com/loopieio/cadence-proxy/internal/cadenceerrors"
	"github.com/loopieio/cadence-proxy/internal/messages"
)

type (

	// WorkflowRegisterReply is a ProxyReply of MessageType
	// WorkflowRegisterReply.  It holds a reference to a ProxyReply in memory
	// and is the reply type to a WorkflowRegisterRequest
	WorkflowRegisterReply struct {
		*ProxyReply
	}
)

// NewWorkflowRegisterReply is the default constructor for
// a WorkflowRegisterReply
//
// returns *WorkflowRegisterReply -> a pointer to a newly initialized
// WorkflowRegisterReply in memory
func NewWorkflowRegisterReply() *WorkflowRegisterReply {
	reply := new(WorkflowRegisterReply)
	reply.ProxyReply = NewProxyReply()
	reply.Type = messages.WorkflowRegisterReply

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (reply *WorkflowRegisterReply) Clone() IProxyMessage {
	WorkflowRegisterReply := NewWorkflowRegisterReply()
	var messageClone IProxyMessage = WorkflowRegisterReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *WorkflowRegisterReply) CopyTo(target IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
}

// SetProxyMessage inherits docs from ProxyMessage.SetProxyMessage()
func (reply *WorkflowRegisterReply) SetProxyMessage(value *ProxyMessage) {
	reply.ProxyMessage.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ProxyMessage.GetProxyMessage()
func (reply *WorkflowRegisterReply) GetProxyMessage() *ProxyMessage {
	return reply.ProxyMessage.GetProxyMessage()
}

// GetRequestID inherits docs from ProxyMessage.GetRequestID()
func (reply *WorkflowRegisterReply) GetRequestID() int64 {
	return reply.ProxyMessage.GetRequestID()
}

// SetRequestID inherits docs from ProxyMessage.SetRequestID()
func (reply *WorkflowRegisterReply) SetRequestID(value int64) {
	reply.ProxyMessage.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from ProxyReply.GetError()
func (reply *WorkflowRegisterReply) GetError() *cadenceerrors.CadenceError {
	return reply.ProxyReply.GetError()
}

// SetError inherits docs from ProxyReply.SetError()
func (reply *WorkflowRegisterReply) SetError(value *cadenceerrors.CadenceError) {
	reply.ProxyReply.SetError(value)
}
