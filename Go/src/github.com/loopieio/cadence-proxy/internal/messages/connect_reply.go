package messages

import (
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

// Clone inherits docs from ProxyReply.Clone()
func (reply *ConnectReply) Clone() IProxyMessage {
	connectReply := NewConnectReply()
	var messageClone IProxyMessage = connectReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyReply.CopyTo()
func (reply *ConnectReply) CopyTo(target IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
}
