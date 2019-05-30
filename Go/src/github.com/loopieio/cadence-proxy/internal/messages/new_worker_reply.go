package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// NewWorkerReply is a ProxyReply of MessageType
	// NewWorkerReply.  It holds a reference to a ProxyReply in memory
	NewWorkerReply struct {
		*ProxyReply
	}
)

// NewNewWorkerReply is the default constructor for
// a NewWorkerReply
//
// returns *NewWorkerReply -> a pointer to a newly initialized
// NewWorkerReply in memory
func NewNewWorkerReply() *NewWorkerReply {
	reply := new(NewWorkerReply)
	reply.ProxyReply = NewProxyReply()
	reply.SetType(messagetypes.NewWorkerReply)

	return reply
}

// GetWorkerID gets the WorkerID property as a int64
// from a NewWorkerReply's properties map
//
// returns int64 -> the WorkerId property in a
// NewWorkerReply's properties map, which represents the new cadence
// worker's ID
func (reply *NewWorkerReply) GetWorkerID() int64 {
	return reply.GetLongProperty("WorkerId")
}

// SetWorkerID sets the WorkerID property in a
// NewWorkerReply's properties map
//
// param value int64 -> WorkerId int64 to be set in a
// NewWorkerReply's properties map.
// It represents the new cadence worker's ID
func (reply *NewWorkerReply) SetWorkerID(value int64) {
	reply.SetLongProperty("WorkerId", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyReply.Clone()
func (reply *NewWorkerReply) Clone() IProxyMessage {
	newWorkerReply := NewNewWorkerReply()
	var messageClone IProxyMessage = newWorkerReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyReply.CopyTo()
func (reply *NewWorkerReply) CopyTo(target IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
	if v, ok := target.(*NewWorkerReply); ok {
		v.SetWorkerID(reply.GetWorkerID())
	}
}
