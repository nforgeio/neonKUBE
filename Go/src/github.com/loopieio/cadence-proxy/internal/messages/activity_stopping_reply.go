package messages

import (
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
