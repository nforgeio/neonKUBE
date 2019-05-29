package messages

import (
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// ActivityInvokeReply is a ActivityReply of MessageType
	// ActivityInvokeReply.  It holds a reference to a ActivityReply in memory
	// and is the reply type to a ActivityInvokeRequest
	ActivityInvokeReply struct {
		*ActivityReply
	}
)

// NewActivityInvokeReply is the default constructor for
// a ActivityInvokeReply
//
// returns *ActivityInvokeReply -> a pointer to a newly initialized
// ActivityInvokeReply in memory
func NewActivityInvokeReply() *ActivityInvokeReply {
	reply := new(ActivityInvokeReply)
	reply.ActivityReply = NewActivityReply()
	reply.SetType(messagetypes.ActivityInvokeReply)

	return reply
}

// GetResult gets the Activity execution result or nil
// from a ActivityInvokeReply's properties map.
// Returns the activity results encoded as a byte array.
//
// returns []byte -> []byte representing the encoded activity results
func (reply *ActivityInvokeReply) GetResult() []byte {
	return reply.GetBytesProperty("Result")
}

// SetResult sets the Activity execution result or nil
// in a ActivityInvokeReply's properties map.
// Returns the activity results encoded as a byte array.
//
// param value []byte -> []byte representing the encoded activity
// results, to be set in the ActivityInvokeReply's properties map
func (reply *ActivityInvokeReply) SetResult(value []byte) {
	reply.SetBytesProperty("Result", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (reply *ActivityInvokeReply) Clone() IProxyMessage {
	activityInvokeReply := NewActivityInvokeReply()
	var messageClone IProxyMessage = activityInvokeReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *ActivityInvokeReply) CopyTo(target IProxyMessage) {
	reply.ActivityReply.CopyTo(target)
	if v, ok := target.(*ActivityInvokeReply); ok {
		v.SetResult(reply.GetResult())
	}
}

// SetProxyMessage inherits docs from ActivityReply.SetProxyMessage()
func (reply *ActivityInvokeReply) SetProxyMessage(value *ProxyMessage) {
	reply.ActivityReply.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ActivityReply.GetProxyMessage()
func (reply *ActivityInvokeReply) GetProxyMessage() *ProxyMessage {
	return reply.ActivityReply.GetProxyMessage()
}

// GetRequestID inherits docs from ActivityReply.GetRequestID()
func (reply *ActivityInvokeReply) GetRequestID() int64 {
	return reply.ActivityReply.GetRequestID()
}

// SetRequestID inherits docs from ActivityReply.SetRequestID()
func (reply *ActivityInvokeReply) SetRequestID(value int64) {
	reply.ActivityReply.SetRequestID(value)
}

// GetType inherits docs from ActivityReply.GetType()
func (reply *ActivityInvokeReply) GetType() messagetypes.MessageType {
	return reply.ActivityReply.GetType()
}

// SetType inherits docs from ActivityReply.SetType()
func (reply *ActivityInvokeReply) SetType(value messagetypes.MessageType) {
	reply.ActivityReply.SetType(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from ActivityReply.GetError()
func (reply *ActivityInvokeReply) GetError() *cadenceerrors.CadenceError {
	return reply.ActivityReply.GetError()
}

// SetError inherits docs from ActivityReply.SetError()
func (reply *ActivityInvokeReply) SetError(value *cadenceerrors.CadenceError) {
	reply.ActivityReply.SetError(value)
}

// -------------------------------------------------------------------------
// IActivityReply interface methods for implementing the IActivityReply interface

// GetContextID inherits docs from ActivityReply.GetContextID()
func (reply *ActivityInvokeReply) GetContextID() int64 {
	return reply.ActivityReply.GetContextID()
}

// SetContextID inherits docs from ActivityReply.GetContextID()
func (reply *ActivityInvokeReply) SetContextID(value int64) {
	reply.ActivityReply.SetContextID(value)
}
