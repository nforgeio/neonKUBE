package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// DomainUpdateReply is a ProxyReply of MessageType
	// DomainUpdateReply.  It holds a reference to a ProxyReply in memory
	DomainUpdateReply struct {
		*ProxyReply
	}
)

// NewDomainUpdateReply is the default constructor for
// a DomainUpdateReply
//
// returns *DomainUpdateReply -> a pointer to a newly initialized
// DomainUpdateReply in memory
func NewDomainUpdateReply() *DomainUpdateReply {
	reply := new(DomainUpdateReply)
	reply.ProxyReply = NewProxyReply()
	reply.SetType(messagetypes.DomainUpdateReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyReply.Clone()
func (reply *DomainUpdateReply) Clone() IProxyMessage {
	domainUpdateReply := NewDomainUpdateReply()
	var messageClone IProxyMessage = domainUpdateReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyReply.CopyTo()
func (reply *DomainUpdateReply) CopyTo(target IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
}
