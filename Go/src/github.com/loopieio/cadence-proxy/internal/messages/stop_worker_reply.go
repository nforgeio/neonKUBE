package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// StopWorkerReply is a ProxyReply of MessageType
	// StopWorkerReply It holds a reference to a
	// ProxyReply in memory
	StopWorkerReply struct {
		*ProxyReply
	}
)

// NewStopWorkerReply is the default constructor for
// a StopWorkerReply
//
// returns *StopWorkerReply -> pointer to a newly initialized
// StopWorkerReply in memory
func NewStopWorkerReply() *StopWorkerReply {
	reply := new(StopWorkerReply)
	reply.ProxyReply = NewProxyReply()
	reply.SetType(messagetypes.StopWorkerReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyReply.Clone()
func (reply *StopWorkerReply) Clone() IProxyMessage {
	stopWorkerReply := NewStopWorkerReply()
	var messageClone IProxyMessage = stopWorkerReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyReply.CopyTo()
func (reply *StopWorkerReply) CopyTo(target IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
}
