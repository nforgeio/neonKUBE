package messages

import (
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
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

// SetProxyMessage inherits docs from ActivityReply.SetProxyMessage()
func (reply *ActivityRecordHeartbeatReply) SetProxyMessage(value *ProxyMessage) {
	reply.ActivityReply.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ActivityReply.GetProxyMessage()
func (reply *ActivityRecordHeartbeatReply) GetProxyMessage() *ProxyMessage {
	return reply.ActivityReply.GetProxyMessage()
}

// GetRequestID inherits docs from ActivityReply.GetRequestID()
func (reply *ActivityRecordHeartbeatReply) GetRequestID() int64 {
	return reply.ActivityReply.GetRequestID()
}

// SetRequestID inherits docs from ActivityReply.SetRequestID()
func (reply *ActivityRecordHeartbeatReply) SetRequestID(value int64) {
	reply.ActivityReply.SetRequestID(value)
}

// GetType inherits docs from ActivityReply.GetType()
func (reply *ActivityRecordHeartbeatReply) GetType() messagetypes.MessageType {
	return reply.ActivityReply.GetType()
}

// SetType inherits docs from ActivityReply.SetType()
func (reply *ActivityRecordHeartbeatReply) SetType(value messagetypes.MessageType) {
	reply.ActivityReply.SetType(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from ActivityReply.GetError()
func (reply *ActivityRecordHeartbeatReply) GetError() *cadenceerrors.CadenceError {
	return reply.ActivityReply.GetError()
}

// SetError inherits docs from ActivityReply.SetError()
func (reply *ActivityRecordHeartbeatReply) SetError(value *cadenceerrors.CadenceError) {
	reply.ActivityReply.SetError(value)
}

// -------------------------------------------------------------------------
// IActivityReply interface methods for implementing the IActivityReply interface

// GetContextID inherits docs from ActivityReply.GetContextID()
func (reply *ActivityRecordHeartbeatReply) GetContextID() int64 {
	return reply.ActivityReply.GetContextID()
}

// SetContextID inherits docs from ActivityReply.GetContextID()
func (reply *ActivityRecordHeartbeatReply) SetContextID(value int64) {
	reply.ActivityReply.SetContextID(value)
}
