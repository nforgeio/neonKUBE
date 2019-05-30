package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// InitializeReply is a ProxyReply of MessageType
	// InitializeReply.  It holds a reference to a ProxyReply in memory
	InitializeReply struct {
		*ProxyReply
	}
)

// NewInitializeReply is the default constructor for
// a InitializeReply
//
// returns *InitializeReply -> a pointer to a newly initialized
// InitializeReply in memory
func NewInitializeReply() *InitializeReply {
	reply := new(InitializeReply)
	reply.ProxyReply = NewProxyReply()
	reply.SetType(messagetypes.InitializeReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyReply.Clone()
func (reply *InitializeReply) Clone() IProxyMessage {
	initializeReply := NewInitializeReply()
	var messageClone IProxyMessage = initializeReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyReply.CopyTo()
func (reply *InitializeReply) CopyTo(target IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
}
