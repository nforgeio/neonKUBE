package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// ActivityHasHeartbeatDetailsReply is a ActivityReply of MessageType
	// ActivityHasHeartbeatDetailsReply.  It holds a reference to a ActivityReply in memory
	// and is the reply type to a ActivityHasHeartbeatDetailsRequest
	ActivityHasHeartbeatDetailsReply struct {
		*ActivityReply
	}
)

// NewActivityHasHeartbeatDetailsReply is the default constructor for
// a ActivityHasHeartbeatDetailsReply
//
// returns *ActivityHasHeartbeatDetailsReply -> a pointer to a newly initialized
// ActivityHasHeartbeatDetailsReply in memory
func NewActivityHasHeartbeatDetailsReply() *ActivityHasHeartbeatDetailsReply {
	reply := new(ActivityHasHeartbeatDetailsReply)
	reply.ActivityReply = NewActivityReply()
	reply.SetType(messagetypes.ActivityHasHeartbeatDetailsReply)

	return reply
}

// GetHasDetails gets the HasDetails property from
// a ActivityHasHeartbeatDetailsReply's properties map.
// Indicates whether heartbeat details are available.
//
// returns bool -> bool indicating whether heartbeat details are available.
func (reply *ActivityHasHeartbeatDetailsReply) GetHasDetails() bool {
	return reply.GetBoolProperty("HasDetails")
}

// SetHasDetails sets the HasDetails property in
// a ActivityHasHeartbeatDetailsReply's properties map.
// Indicates whether heartbeat details are available.
//
// param value bool -> bool indicating whether heartbeat details are available,
// to be set in the ActivityHasHeartbeatDetailsReply's properties map
func (reply *ActivityHasHeartbeatDetailsReply) SetHasDetails(value bool) {
	reply.SetBoolProperty("HasDetails", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (reply *ActivityHasHeartbeatDetailsReply) Clone() IProxyMessage {
	activityHasHeartbeatDetailsReply := NewActivityHasHeartbeatDetailsReply()
	var messageClone IProxyMessage = activityHasHeartbeatDetailsReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *ActivityHasHeartbeatDetailsReply) CopyTo(target IProxyMessage) {
	reply.ActivityReply.CopyTo(target)
	if v, ok := target.(*ActivityHasHeartbeatDetailsReply); ok {
		v.SetHasDetails(reply.GetHasDetails())
	}
}
