package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// ActivityInvokeLocalReply is a ActivityReply of MessageType
	// ActivityInvokeLocalReply.  It holds a reference to a ActivityReply in memory
	// and is the reply type to a ActivityInvokeLocalRequest
	ActivityInvokeLocalReply struct {
		*ActivityReply
	}
)

// NewActivityInvokeLocalReply is the default constructor for
// a ActivityInvokeLocalReply
//
// returns *ActivityInvokeLocalReply -> a pointer to a newly initialized
// ActivityInvokeLocalReply in memory
func NewActivityInvokeLocalReply() *ActivityInvokeLocalReply {
	reply := new(ActivityInvokeLocalReply)
	reply.ActivityReply = NewActivityReply()
	reply.SetType(messagetypes.ActivityInvokeLocalReply)

	return reply
}

// GetResult gets the activity results encoded as a byte array result or nil
// from a ActivityInvokeLocalReply's properties map.
//
// returns []byte -> []byte representing the result of a Activity execution
func (reply *ActivityInvokeLocalReply) GetResult() []byte {
	return reply.GetBytesProperty("Result")
}

// SetResult sets the activity results encoded as a byte array result or nil
// in a ActivityInvokeLocalReply's properties map.
//
// param value []byte -> []byte representing the result of a Activity execution
// to be set in the ActivityInvokeLocalReply's properties map
func (reply *ActivityInvokeLocalReply) SetResult(value []byte) {
	reply.SetBytesProperty("Result", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ActivityReply.Clone()
func (reply *ActivityInvokeLocalReply) Clone() IProxyMessage {
	activityInvokeLocalReply := NewActivityInvokeLocalReply()
	var messageClone IProxyMessage = activityInvokeLocalReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ActivityReply.CopyTo()
func (reply *ActivityInvokeLocalReply) CopyTo(target IProxyMessage) {
	reply.ActivityReply.CopyTo(target)
	if v, ok := target.(*ActivityInvokeLocalReply); ok {
		v.SetResult(reply.GetResult())
	}
}
