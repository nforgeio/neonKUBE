package messages

import (
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
