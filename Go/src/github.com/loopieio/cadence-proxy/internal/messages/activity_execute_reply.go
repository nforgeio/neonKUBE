package messages

import (
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// ActivityExecuteReply is a ActivityReply of MessageType
	// ActivityExecuteReply.  It holds a reference to a ActivityReply in memory
	// and is the reply type to a ActivityExecuteRequest
	ActivityExecuteReply struct {
		*ActivityReply
	}
)

// NewActivityExecuteReply is the default constructor for
// a ActivityExecuteReply
//
// returns *ActivityExecuteReply -> a pointer to a newly initialized
// ActivityExecuteReply in memory
func NewActivityExecuteReply() *ActivityExecuteReply {
	reply := new(ActivityExecuteReply)
	reply.ActivityReply = NewActivityReply()
	reply.SetType(messagetypes.ActivityExecuteReply)

	return reply
}

// GetResult gets the Activity execution result or nil
// from a ActivityExecuteReply's properties map.
//
// returns []byte -> []byte representing the result of a Activity execution
func (reply *ActivityExecuteReply) GetResult() []byte {
	return reply.GetBytesProperty("Result")
}

// SetResult sets the Activity execution result or nil
// in a ActivityExecuteReply's properties map.
//
// param value []byte -> []byte representing the result of a Activity execution
// to be set in the ActivityExecuteReply's properties map
func (reply *ActivityExecuteReply) SetResult(value []byte) {
	reply.SetBytesProperty("Result", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (reply *ActivityExecuteReply) Clone() IProxyMessage {
	activityExecuteReply := NewActivityExecuteReply()
	var messageClone IProxyMessage = activityExecuteReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *ActivityExecuteReply) CopyTo(target IProxyMessage) {
	reply.ActivityReply.CopyTo(target)
	if v, ok := target.(*ActivityExecuteReply); ok {
		v.SetResult(reply.GetResult())
	}
}

// SetProxyMessage inherits docs from ActivityReply.SetProxyMessage()
func (reply *ActivityExecuteReply) SetProxyMessage(value *ProxyMessage) {
	reply.ActivityReply.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ActivityReply.GetProxyMessage()
func (reply *ActivityExecuteReply) GetProxyMessage() *ProxyMessage {
	return reply.ActivityReply.GetProxyMessage()
}

// GetRequestID inherits docs from ActivityReply.GetRequestID()
func (reply *ActivityExecuteReply) GetRequestID() int64 {
	return reply.ActivityReply.GetRequestID()
}

// SetRequestID inherits docs from ActivityReply.SetRequestID()
func (reply *ActivityExecuteReply) SetRequestID(value int64) {
	reply.ActivityReply.SetRequestID(value)
}

// GetType inherits docs from ActivityReply.GetType()
func (reply *ActivityExecuteReply) GetType() messagetypes.MessageType {
	return reply.ActivityReply.GetType()
}

// SetType inherits docs from ActivityReply.SetType()
func (reply *ActivityExecuteReply) SetType(value messagetypes.MessageType) {
	reply.ActivityReply.SetType(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from ActivityReply.GetError()
func (reply *ActivityExecuteReply) GetError() *cadenceerrors.CadenceError {
	return reply.ActivityReply.GetError()
}

// SetError inherits docs from ActivityReply.SetError()
func (reply *ActivityExecuteReply) SetError(value *cadenceerrors.CadenceError) {
	reply.ActivityReply.SetError(value)
}

// -------------------------------------------------------------------------
// IActivityReply interface methods for implementing the IActivityReply interface

// GetContextID inherits docs from ActivityReply.GetContextID()
func (reply *ActivityExecuteReply) GetContextID() int64 {
	return reply.ActivityReply.GetContextID()
}

// SetContextID inherits docs from ActivityReply.GetContextID()
func (reply *ActivityExecuteReply) SetContextID(value int64) {
	reply.ActivityReply.SetContextID(value)
}
