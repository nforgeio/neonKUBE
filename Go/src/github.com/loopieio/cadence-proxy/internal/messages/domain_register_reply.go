package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// DomainRegisterReply is a ProxyReply of MessageType
	// DomainRegisterReply.  It holds a reference to a ProxyReply in memory
	DomainRegisterReply struct {
		*ProxyReply
	}
)

// NewDomainRegisterReply is the default constructor for
// a DomainRegisterReply
//
// returns *DomainRegisterReply -> a pointer to a newly initialized
// DomainRegisterReply in memory
func NewDomainRegisterReply() *DomainRegisterReply {
	reply := new(DomainRegisterReply)
	reply.ProxyReply = NewProxyReply()
	reply.SetType(messagetypes.DomainRegisterReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyReply.Clone()
func (reply *DomainRegisterReply) Clone() IProxyMessage {
	domainRegisterReply := NewDomainRegisterReply()
	var messageClone IProxyMessage = domainRegisterReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyReply.CopyTo()
func (reply *DomainRegisterReply) CopyTo(target IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
}
