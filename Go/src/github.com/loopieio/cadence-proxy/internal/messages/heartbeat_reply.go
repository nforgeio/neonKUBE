package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// HeartbeatReply is a ProxyReply of MessageType
	// HeartbeatReply It holds a reference to a
	// ProxyReply in memory
	HeartbeatReply struct {
		*ProxyReply
	}
)

// NewHeartbeatReply is the default constructor for
// a HeartbeatReply
//
// returns *HeartbeatReply -> pointer to a newly initialized
// HeartbeatReply in memory
func NewHeartbeatReply() *HeartbeatReply {
	reply := new(HeartbeatReply)
	reply.ProxyReply = NewProxyReply()
	reply.SetType(messagetypes.HeartbeatReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyReply.Clone()
func (reply *HeartbeatReply) Clone() IProxyMessage {
	heartbeatReply := NewHeartbeatReply()
	var messageClone IProxyMessage = heartbeatReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyReply.CopyTo()
func (reply *HeartbeatReply) CopyTo(target IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
}
