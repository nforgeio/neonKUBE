package messages

import (
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
