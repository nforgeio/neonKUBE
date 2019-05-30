package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
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
	reply.SetType(messagetypes.TerminateReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyReply.Clone()
func (reply *TerminateReply) Clone() IProxyMessage {
	terminateReply := NewTerminateReply()
	var messageClone IProxyMessage = terminateReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyReply.CopyTo()
func (reply *TerminateReply) CopyTo(target IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
}
