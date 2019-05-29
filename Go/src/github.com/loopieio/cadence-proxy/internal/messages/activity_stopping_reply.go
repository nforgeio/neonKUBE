package messages

import (
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// ActivityStoppingReply is a ActivityReply of MessageType
	// ActivityStoppingReply.  It holds a reference to a ActivityReply in memory
	// and is the reply type to a ActivityStoppingRequest
	ActivityStoppingReply struct {
		*ActivityReply
	}
)

// NewActivityStoppingReply is the default constructor for
// a ActivityStoppingReply
//
// returns *ActivityStoppingReply -> a pointer to a newly initialized
// ActivityStoppingReply in memory
func NewActivityStoppingReply() *ActivityStoppingReply {
	reply := new(ActivityStoppingReply)
	reply.ActivityReply = NewActivityReply()
	reply.SetType(messagetypes.ActivityStoppingReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (reply *ActivityStoppingReply) Clone() IProxyMessage {
	activityStoppingReply := NewActivityStoppingReply()
	var messageClone IProxyMessage = activityStoppingReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *ActivityStoppingReply) CopyTo(target IProxyMessage) {
	reply.ActivityReply.CopyTo(target)
}

// SetProxyMessage inherits docs from ActivityReply.SetProxyMessage()
func (reply *ActivityStoppingReply) SetProxyMessage(value *ProxyMessage) {
	reply.ActivityReply.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ActivityReply.GetProxyMessage()
func (reply *ActivityStoppingReply) GetProxyMessage() *ProxyMessage {
	return reply.ActivityReply.GetProxyMessage()
}

// GetRequestID inherits docs from ActivityReply.GetRequestID()
func (reply *ActivityStoppingReply) GetRequestID() int64 {
	return reply.ActivityReply.GetRequestID()
}

// SetRequestID inherits docs from ActivityReply.SetRequestID()
func (reply *ActivityStoppingReply) SetRequestID(value int64) {
	reply.ActivityReply.SetRequestID(value)
}

// GetType inherits docs from ActivityReply.GetType()
func (reply *ActivityStoppingReply) GetType() messagetypes.MessageType {
	return reply.ActivityReply.GetType()
}

// SetType inherits docs from ActivityReply.SetType()
func (reply *ActivityStoppingReply) SetType(value messagetypes.MessageType) {
	reply.ActivityReply.SetType(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from ActivityReply.GetError()
func (reply *ActivityStoppingReply) GetError() *cadenceerrors.CadenceError {
	return reply.ActivityReply.GetError()
}

// SetError inherits docs from ActivityReply.SetError()
func (reply *ActivityStoppingReply) SetError(value *cadenceerrors.CadenceError) {
	reply.ActivityReply.SetError(value)
}

// -------------------------------------------------------------------------
// IActivityReply interface methods for implementing the IActivityReply interface

// GetContextID inherits docs from ActivityReply.GetContextID()
func (reply *ActivityStoppingReply) GetContextID() int64 {
	return reply.ActivityReply.GetContextID()
}

// SetContextID inherits docs from ActivityReply.GetContextID()
func (reply *ActivityStoppingReply) SetContextID(value int64) {
	reply.ActivityReply.SetContextID(value)
}
