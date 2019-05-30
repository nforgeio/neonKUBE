package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// ActivityRecordHeartbeatReply is a ActivityReply of MessageType
	// ActivityRecordHeartbeatReply.  It holds a reference to a ActivityReply in memory
	// and is the reply type to a ActivityRecordHeartbeatRequest
	ActivityRecordHeartbeatReply struct {
		*ActivityReply
	}
)

// NewActivityRecordHeartbeatReply is the default constructor for
// a ActivityRecordHeartbeatReply
//
// returns *ActivityRecordHeartbeatReply -> a pointer to a newly initialized
// ActivityRecordHeartbeatReply in memory
func NewActivityRecordHeartbeatReply() *ActivityRecordHeartbeatReply {
	reply := new(ActivityRecordHeartbeatReply)
	reply.ActivityReply = NewActivityReply()
	reply.SetType(messagetypes.ActivityRecordHeartbeatReply)

	return reply
}

// GetDetails gets the Activity heartbeat Details or nil
// from a ActivityRecordHeartbeatReply's properties map.
// Returns the activity heartbeat details encoded as a byte array.
//
// returns []byte -> []byte representing the encoded activity heartbeat Details
func (reply *ActivityRecordHeartbeatReply) GetDetails() []byte {
	return reply.GetBytesProperty("Details")
}

// SetDetails sets the Activity heartbeat Details or nil
// in a ActivityRecordHeartbeatReply's properties map.
// Returns the activity heartbeat details encoded as a byte array.
//
// param value []byte -> []byte representing the encoded activity heartbeat
// Details, to be set in the ActivityRecordHeartbeatReply's properties map
func (reply *ActivityRecordHeartbeatReply) SetDetails(value []byte) {
	reply.SetBytesProperty("Details", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (reply *ActivityRecordHeartbeatReply) Clone() IProxyMessage {
	activityRecordHeartbeatReply := NewActivityRecordHeartbeatReply()
	var messageClone IProxyMessage = activityRecordHeartbeatReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *ActivityRecordHeartbeatReply) CopyTo(target IProxyMessage) {
	reply.ActivityReply.CopyTo(target)
	if v, ok := target.(*ActivityRecordHeartbeatReply); ok {
		v.SetDetails(reply.GetDetails())
	}
}
