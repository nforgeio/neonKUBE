package messages

import (
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// PingReply is a ProxyReply of MessageType
	// PingReply It holds a reference to a
	// ProxyReply in memory
	PingReply struct {
		*ProxyReply
	}
)

// NewPingReply is the default constructor for
// a PingReply
//
// returns *PingReply -> pointer to a newly initialized
// PingReply in memory
func NewPingReply() *PingReply {
	reply := new(PingReply)
	reply.ProxyReply = NewProxyReply()
	reply.SetType(messagetypes.PingReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyReply.Clone()
func (reply *PingReply) Clone() IProxyMessage {
	pingReply := NewPingReply()
	var messageClone IProxyMessage = pingReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyReply.CopyTo()
func (reply *PingReply) CopyTo(target IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
}

// SetProxyMessage inherits docs from ProxyReply.SetProxyMessage()
func (reply *PingReply) SetProxyMessage(value *ProxyMessage) {
	reply.ProxyReply.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ProxyReply.GetProxyMessage()
func (reply *PingReply) GetProxyMessage() *ProxyMessage {
	return reply.ProxyReply.GetProxyMessage()
}

// GetRequestID inherits docs from ProxyReply.GetRequestID()
func (reply *PingReply) GetRequestID() int64 {
	return reply.ProxyReply.GetRequestID()
}

// SetRequestID inherits docs from ProxyReply.SetRequestID()
func (reply *PingReply) SetRequestID(value int64) {
	reply.ProxyReply.SetRequestID(value)
}

// GetType inherits docs from ProxyReply.GetType()
func (reply *PingReply) GetType() messagetypes.MessageType {
	return reply.ProxyReply.GetType()
}

// SetType inherits docs from ProxyReply.SetType()
func (reply *PingReply) SetType(value messagetypes.MessageType) {
	reply.ProxyReply.SetType(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from ProxyReply.GetError()
func (reply *PingReply) GetError() *cadenceerrors.CadenceError {
	return reply.ProxyReply.GetError()
}

// SetError inherits docs from ProxyReply.SetError()
func (reply *PingReply) SetError(value *cadenceerrors.CadenceError) {
	reply.ProxyReply.SetError(value)
}
