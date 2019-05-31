package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// ActivityRegisterReply is a ActivityReply of MessageType
	// ActivityRegisterReply.  It holds a reference to a ActivityReply in memory
	// and is the reply type to a ActivityRegisterRequest
	ActivityRegisterReply struct {
		*ActivityReply
	}
)

// NewActivityRegisterReply is the default constructor for
// a ActivityRegisterReply
//
// returns *ActivityRegisterReply -> a pointer to a newly initialized
// ActivityRegisterReply in memory
func NewActivityRegisterReply() *ActivityRegisterReply {
	reply := new(ActivityRegisterReply)
	reply.ActivityReply = NewActivityReply()
	reply.SetType(messagetypes.ActivityRegisterReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (reply *ActivityRegisterReply) Clone() IProxyMessage {
	activityRegisterReply := NewActivityRegisterReply()
	var messageClone IProxyMessage = activityRegisterReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *ActivityRegisterReply) CopyTo(target IProxyMessage) {
	reply.ActivityReply.CopyTo(target)
}
