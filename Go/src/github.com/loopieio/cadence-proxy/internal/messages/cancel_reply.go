package messages

import (
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
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
	reply.SetType(messagetypes.CancelReply)

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

// Clone inherits docs from ProxyReply.Clone()
func (reply *CancelReply) Clone() IProxyMessage {
	cancelReply := NewCancelReply()
	var messageClone IProxyMessage = cancelReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyReply.CopyTo()
func (reply *CancelReply) CopyTo(target IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
	if v, ok := target.(*CancelReply); ok {
		v.SetWasCancelled(reply.GetWasCancelled())
	}
}

// SetProxyMessage inherits docs from ProxyReply.SetProxyMessage()
func (reply *CancelReply) SetProxyMessage(value *ProxyMessage) {
	reply.ProxyReply.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ProxyReply.GetProxyMessage()
func (reply *CancelReply) GetProxyMessage() *ProxyMessage {
	return reply.ProxyReply.GetProxyMessage()
}

// GetRequestID inherits docs from ProxyReply.GetRequestID()
func (reply *CancelReply) GetRequestID() int64 {
	return reply.ProxyReply.GetRequestID()
}

// SetRequestID inherits docs from ProxyReply.SetRequestID()
func (reply *CancelReply) SetRequestID(value int64) {
	reply.ProxyReply.SetRequestID(value)
}

// GetType inherits docs from ProxyReply.GetType()
func (reply *CancelReply) GetType() messagetypes.MessageType {
	return reply.ProxyReply.GetType()
}

// SetType inherits docs from ProxyReply.SetType()
func (reply *CancelReply) SetType(value messagetypes.MessageType) {
	reply.ProxyReply.SetType(value)
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
