package types

import (
	"github.com/loopieio/cadence-proxy/internal/cadenceerrors"
	"github.com/loopieio/cadence-proxy/internal/messages"
)

type (

	// TerminateReply is a ProxyReply of MessageType
	// TerminateReply It holds a reference to a
	// ProxyReply in memory
	TerminateReply struct {
		*ProxyReply
	}
)

// NewTerminateReply is the default constructor for
// a TerminateReply
//
// returns *TerminateReply -> pointer to a newly initialized
// TerminateReply in memory
func NewTerminateReply() *TerminateReply {
	reply := new(TerminateReply)
	reply.ProxyReply = NewProxyReply()
	reply.Type = messages.TerminateReply

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (reply *TerminateReply) Clone() IProxyMessage {
	terminateReply := NewTerminateReply()
	var messageClone IProxyMessage = terminateReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *TerminateReply) CopyTo(target IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
}

// SetProxyMessage inherits docs from ProxyMessage.SetProxyMessage()
func (reply *TerminateReply) SetProxyMessage(value *ProxyMessage) {
	reply.ProxyMessage.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ProxyMessage.GetProxyMessage()
func (reply *TerminateReply) GetProxyMessage() *ProxyMessage {
	return reply.ProxyMessage.GetProxyMessage()
}

// GetRequestID inherits docs from ProxyMessage.GetRequestID()
func (reply *TerminateReply) GetRequestID() int64 {
	return reply.ProxyMessage.GetRequestID()
}

// SetRequestID inherits docs from ProxyMessage.SetRequestID()
func (reply *TerminateReply) SetRequestID(value int64) {
	reply.ProxyMessage.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from ProxyReply.GetError()
func (reply *TerminateReply) GetError() *cadenceerrors.CadenceError {
	return reply.ProxyReply.GetError()
}

// SetError inherits docs from ProxyReply.SetError()
func (reply *TerminateReply) SetError(value *cadenceerrors.CadenceError) {
	reply.ProxyReply.SetError(value)
}
