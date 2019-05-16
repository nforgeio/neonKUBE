package messages

import (
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// ConnectReply is a ProxyReply of MessageType
	// ConnectReply.  It holds a reference to a ProxyReply in memory
	ConnectReply struct {
		*ProxyReply
	}
)

// NewConnectReply is the default constructor for
// a ConnectReply
//
// returns *ConnectReply -> a pointer to a newly initialized
// ConnectReply in memory
func NewConnectReply() *ConnectReply {
	reply := new(ConnectReply)
	reply.ProxyReply = NewProxyReply()
	reply.Type = messagetypes.ConnectReply

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from IProxyMessage.Clone()
func (reply *ConnectReply) Clone() IProxyMessage {
	connectReply := NewConnectReply()
	var messageClone IProxyMessage = connectReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from IProxyMessage.CopyTo()
func (reply *ConnectReply) CopyTo(target IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
}

// SetProxyMessage inherits docs from IProxyMessage.SetProxyMessage()
func (reply *ConnectReply) SetProxyMessage(value *ProxyMessage) {
	reply.ProxyReply.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from IProxyMessage.GetProxyMessage()
func (reply *ConnectReply) GetProxyMessage() *ProxyMessage {
	return reply.ProxyReply.GetProxyMessage()
}

// GetRequestID inherits docs from IProxyMessage.GetRequestID()
func (reply *ConnectReply) GetRequestID() int64 {
	return reply.ProxyReply.GetRequestID()
}

// SetRequestID inherits docs from IProxyMessage.SetRequestID()
func (reply *ConnectReply) SetRequestID(value int64) {
	reply.ProxyReply.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from ProxyReply.GetError()
func (reply *ConnectReply) GetError() *cadenceerrors.CadenceError {
	return reply.ProxyReply.GetError()
}

// SetError inherits docs from ProxyReply.SetError()
func (reply *ConnectReply) SetError(value *cadenceerrors.CadenceError) {
	reply.ProxyReply.SetError(value)
}
