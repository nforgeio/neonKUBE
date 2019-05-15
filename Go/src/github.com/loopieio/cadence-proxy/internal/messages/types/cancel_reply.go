package types

import (
	"github.com/loopieio/cadence-proxy/internal/cadenceerrors"
	"github.com/loopieio/cadence-proxy/internal/messages"
)

type (

	// CancelReply is a ProxyReply of MessageType
	// CancelReply.  It holds a reference to a ProxyReply in memory
	CancelReply struct {
		*ProxyReply
	}
)

// NewCancelReply is the default constructor for
// a CancelReply
//
// returns *CancelReply -> a pointer to a newly initialized
// CancelReply in memory
func NewCancelReply() *CancelReply {
	reply := new(CancelReply)
	reply.ProxyReply = NewProxyReply()
	reply.Type = messages.CancelReply

	return reply
}

// GetWasCancelled gets the WasCancelled property as a bool
// from a CancelReply's properties map
//
// returns bool -> a boolean from a CancelReply's properties map
// that indicates if an operation has been cancelled
func (reply *CancelReply) GetWasCancelled() bool {
	return reply.GetBoolProperty("WasCancelled")
}

// SetWasCancelled sets the WasCancelled property in a
// CancelReply's properties map
//
// param value bool -> the bool value to set as the WasCancelled
// property in a CancelReply's properties map
func (reply *CancelReply) SetWasCancelled(value bool) {
	reply.SetBoolProperty("WasCancelled", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (reply *CancelReply) Clone() IProxyMessage {
	cancelReply := NewCancelReply()
	var messageClone IProxyMessage = cancelReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *CancelReply) CopyTo(target IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
	if v, ok := target.(*CancelReply); ok {
		v.SetWasCancelled(reply.GetWasCancelled())
	}
}

// SetProxyMessage inherits docs from ProxyMessage.SetProxyMessage()
func (reply *CancelReply) SetProxyMessage(value *ProxyMessage) {
	reply.ProxyMessage.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ProxyMessage.GetProxyMessage()
func (reply *CancelReply) GetProxyMessage() *ProxyMessage {
	return reply.ProxyMessage.GetProxyMessage()
}

// GetRequestID inherits docs from ProxyMessage.GetRequestID()
func (reply *CancelReply) GetRequestID() int64 {
	return reply.ProxyMessage.GetRequestID()
}

// SetRequestID inherits docs from ProxyMessage.SetRequestID()
func (reply *CancelReply) SetRequestID(value int64) {
	reply.ProxyMessage.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from ProxyReply.GetError()
func (reply *CancelReply) GetError() *cadenceerrors.CadenceError {
	return reply.ProxyReply.GetError()
}

// SetError inherits docs from ProxyReply.SetError()
func (reply *CancelReply) SetError(value *cadenceerrors.CadenceError) {
	reply.ProxyReply.SetError(value)
}
