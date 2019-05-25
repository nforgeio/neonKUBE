package messages

import (
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// ActivityRegisterReply is a ActivityReply of MessageType
	// ActivityRegisterReply.  It holds a reference to a ActivityReply in memory
	ActivityRegisterReply struct {
		*ActivityReply
	}
)

// NewActivityRegisterReply is the default constructor for ActivityRegisterReply.
// It creates a new ActivityRegisterReply in memory and then creates and sets
// a reference to a new ActivityReply in the ActivityRegisterReply.
//
// returns *ActivityRegisterReply -> a pointer to a new ActivityRegisterReply in memory
func NewActivityRegisterReply() *ActivityRegisterReply {
	reply := new(ActivityRegisterReply)
	reply.ActivityReply = NewActivityReply()
	reply.SetType(messagetypes.ActivityRegisterReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ActivityReply.Clone()
func (reply *ActivityRegisterReply) Clone() IProxyMessage {
	activityRegisterReply := NewActivityRegisterReply()
	var messageClone IProxyMessage = activityRegisterReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ActivityReply.CopyTo()
func (reply *ActivityRegisterReply) CopyTo(target IProxyMessage) {
	reply.ActivityReply.CopyTo(target)
	if v, ok := target.(*ActivityRegisterReply); ok {
		v.SetActivityContextID(reply.GetActivityContextID())
	}
}

// SetProxyMessage inherits docs from ActivityReply.SetProxyMessage()
func (reply *ActivityRegisterReply) SetProxyMessage(value *ProxyMessage) {
	reply.ActivityReply.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ActivityReply.GetProxyMessage()
func (reply *ActivityRegisterReply) GetProxyMessage() *ProxyMessage {
	return reply.ActivityReply.GetProxyMessage()
}

// GetRequestID inherits docs from ActivityReply.GetRequestID()
func (reply *ActivityRegisterReply) GetRequestID() int64 {
	return reply.ActivityReply.GetRequestID()
}

// SetRequestID inherits docs from ActivityReply.SetRequestID()
func (reply *ActivityRegisterReply) SetRequestID(value int64) {
	reply.ActivityReply.SetRequestID(value)
}

// GetType inherits docs from ActivityReply.GetType()
func (reply *ActivityRegisterReply) GetType() messagetypes.MessageType {
	return reply.ActivityReply.GetType()
}

// SetType inherits docs from ActivityReply.SetType()
func (reply *ActivityRegisterReply) SetType(value messagetypes.MessageType) {
	reply.ActivityReply.SetType(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from IProxyReply.GetError()
func (reply *ActivityRegisterReply) GetError() *cadenceerrors.CadenceError {
	return reply.ActivityReply.GetError()
}

// SetError inherits docs from IProxyReply.SetError()
func (reply *ActivityRegisterReply) SetError(value *cadenceerrors.CadenceError) {
	reply.ActivityReply.SetError(value)
}

// -------------------------------------------------------------------------
// IActivityReply interface methods for implementing the IActivityReply interface

// GetActivityContextID inherits docs from ActivityReply.GetActivityContextID()
func (reply *ActivityRegisterReply) GetActivityContextID() int64 {
	return reply.ActivityReply.GetActivityContextID()
}

// SetActivityContextID inherits docs from ActivityReply.SetActivityContextID()
func (reply *ActivityRegisterReply) SetActivityContextID(value int64) {
	reply.ActivityReply.SetActivityContextID(value)
}
