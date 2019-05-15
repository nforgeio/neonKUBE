package types

import (
	"github.com/loopieio/cadence-proxy/internal/cadenceerrors"
	"github.com/loopieio/cadence-proxy/internal/messages"
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
	reply.Type = messages.DomainRegisterReply

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (reply *DomainRegisterReply) Clone() IProxyMessage {
	domainRegisterReply := NewDomainRegisterReply()
	var messageClone IProxyMessage = domainRegisterReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *DomainRegisterReply) CopyTo(target IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
}

// SetProxyMessage inherits docs from ProxyMessage.SetProxyMessage()
func (reply *DomainRegisterReply) SetProxyMessage(value *ProxyMessage) {
	reply.ProxyMessage.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ProxyMessage.GetProxyMessage()
func (reply *DomainRegisterReply) GetProxyMessage() *ProxyMessage {
	return reply.ProxyMessage.GetProxyMessage()
}

// GetRequestID inherits docs from ProxyMessage.GetRequestID()
func (reply *DomainRegisterReply) GetRequestID() int64 {
	return reply.ProxyMessage.GetRequestID()
}

// SetRequestID inherits docs from ProxyMessage.SetRequestID()
func (reply *DomainRegisterReply) SetRequestID(value int64) {
	reply.ProxyMessage.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from ProxyReply.GetError()
func (reply *DomainRegisterReply) GetError() *cadenceerrors.CadenceError {
	return reply.ProxyReply.GetError()
}

// SetError inherits docs from ProxyReply.SetError()
func (reply *DomainRegisterReply) SetError(value *cadenceerrors.CadenceError) {
	reply.ProxyReply.SetError(value)
}
