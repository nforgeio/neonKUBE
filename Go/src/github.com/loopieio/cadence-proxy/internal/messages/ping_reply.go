package messages

import (
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
