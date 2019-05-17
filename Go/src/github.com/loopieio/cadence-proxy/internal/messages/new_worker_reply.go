package messages

import (
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
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
	reply.Type = messagetypes.NewWorkerReply

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

// Clone inherits docs from ProxyMessage.Clone()
func (reply *NewWorkerReply) Clone() IProxyMessage {
	newWorkerReply := NewNewWorkerReply()
	var messageClone IProxyMessage = newWorkerReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *NewWorkerReply) CopyTo(target IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
	if v, ok := target.(*NewWorkerReply); ok {
		v.SetWorkerID(reply.GetWorkerID())
	}
}

// SetProxyMessage inherits docs from ProxyMessage.SetProxyMessage()
func (reply *NewWorkerReply) SetProxyMessage(value *ProxyMessage) {
	reply.ProxyReply.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ProxyMessage.GetProxyMessage()
func (reply *NewWorkerReply) GetProxyMessage() *ProxyMessage {
	return reply.ProxyReply.GetProxyMessage()
}

// GetRequestID inherits docs from ProxyMessage.GetRequestID()
func (reply *NewWorkerReply) GetRequestID() int64 {
	return reply.ProxyReply.GetRequestID()
}

// SetRequestID inherits docs from ProxyMessage.SetRequestID()
func (reply *NewWorkerReply) SetRequestID(value int64) {
	reply.ProxyReply.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from ProxyReply.GetError()
func (reply *NewWorkerReply) GetError() *cadenceerrors.CadenceError {
	return reply.ProxyReply.GetError()
}

// SetError inherits docs from ProxyReply.SetError()
func (reply *NewWorkerReply) SetError(value *cadenceerrors.CadenceError) {
	reply.ProxyReply.SetError(value)
}
