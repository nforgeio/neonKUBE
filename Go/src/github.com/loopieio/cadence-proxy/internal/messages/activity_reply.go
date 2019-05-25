package messages

import (
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// ActivityReply is base type for all workflow replies.
	// All workflow replies will inherit from ActivityReply
	//
	// A ActivityReply contains a reference to a
	// ProxyReply struct in memory
	ActivityReply struct {
		*ProxyReply
	}

	// IActivityReply is the interface that all workflow message replies
	// implement.
	IActivityReply interface {
		GetActivityContextID() int64
		SetActivityContextID(value int64)
		GetError() *cadenceerrors.CadenceError
		SetError(value *cadenceerrors.CadenceError)
		Clone() IProxyMessage
		CopyTo(target IProxyMessage)
		SetProxyMessage(value *ProxyMessage)
		GetProxyMessage() *ProxyMessage
		GetRequestID() int64
		SetRequestID(int64)
		GetType() messagetypes.MessageType
		SetType(value messagetypes.MessageType)
	}
)

// NewActivityReply is the default constructor for ActivityReply.
// It creates a new ActivityReply in memory and then creates and sets
// a reference to a new ProxyReply in the ActivityReply.
//
// returns *ActivityReply -> a pointer to a new ActivityReply in memory
func NewActivityReply() *ActivityReply {
	reply := new(ActivityReply)
	reply.ProxyReply = NewProxyReply()
	reply.SetType(messagetypes.Unspecified)

	return reply
}

// -------------------------------------------------------------------------
// IActivityReply interface methods for implementing the IActivityReply interface

// GetActivityContextID gets the ContextId from a ActivityReply's properties
// map.
//
// returns int64 -> the long representing a ActivityReply's ContextId
func (reply *ActivityReply) GetActivityContextID() int64 {
	return reply.GetLongProperty("ActivityContextId")
}

// SetActivityContextID sets the ContextId in a ActivityReply's properties map
//
// param value int64 -> int64 value to set as the ActivityReply's ContextId
// in its properties map
func (reply *ActivityReply) SetActivityContextID(value int64) {
	reply.SetLongProperty("ActivityContextId", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyReply.Clone()
func (reply *ActivityReply) Clone() IProxyMessage {
	workflowContextReply := NewActivityReply()
	var messageClone IProxyMessage = workflowContextReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyReply.CopyTo()
func (reply *ActivityReply) CopyTo(target IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
	if v, ok := target.(IActivityReply); ok {
		v.SetActivityContextID(reply.GetActivityContextID())
	}
}

// SetProxyMessage inherits docs from ProxyReply.SetProxyMessage()
func (reply *ActivityReply) SetProxyMessage(value *ProxyMessage) {
	reply.ProxyReply.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ProxyReply.GetProxyMessage()
func (reply *ActivityReply) GetProxyMessage() *ProxyMessage {
	return reply.ProxyReply.GetProxyMessage()
}

// GetRequestID inherits docs from ProxyReply.GetRequestID()
func (reply *ActivityReply) GetRequestID() int64 {
	return reply.ProxyReply.GetRequestID()
}

// SetRequestID inherits docs from ProxyReply.SetRequestID()
func (reply *ActivityReply) SetRequestID(value int64) {
	reply.ProxyReply.SetRequestID(value)
}

// GetType inherits docs from ProxyReply.GetType()
func (reply *ActivityReply) GetType() messagetypes.MessageType {
	return reply.ProxyReply.GetType()
}

// SetType inherits docs from ProxyReply.SetType()
func (reply *ActivityReply) SetType(value messagetypes.MessageType) {
	reply.ProxyReply.SetType(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from IProxyReply.GetError()
func (reply *ActivityReply) GetError() *cadenceerrors.CadenceError {
	return reply.ProxyReply.GetError()
}

// SetError inherits docs from IProxyReply.SetError()
func (reply *ActivityReply) SetError(value *cadenceerrors.CadenceError) {
	reply.ProxyReply.SetError(value)
}
