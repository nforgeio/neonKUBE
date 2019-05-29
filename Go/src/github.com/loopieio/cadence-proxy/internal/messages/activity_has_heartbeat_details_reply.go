package messages

import (
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
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

// SetProxyMessage inherits docs from ActivityReply.SetProxyMessage()
func (reply *ActivityHasHeartbeatDetailsReply) SetProxyMessage(value *ProxyMessage) {
	reply.ActivityReply.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ActivityReply.GetProxyMessage()
func (reply *ActivityHasHeartbeatDetailsReply) GetProxyMessage() *ProxyMessage {
	return reply.ActivityReply.GetProxyMessage()
}

// GetRequestID inherits docs from ActivityReply.GetRequestID()
func (reply *ActivityHasHeartbeatDetailsReply) GetRequestID() int64 {
	return reply.ActivityReply.GetRequestID()
}

// SetRequestID inherits docs from ActivityReply.SetRequestID()
func (reply *ActivityHasHeartbeatDetailsReply) SetRequestID(value int64) {
	reply.ActivityReply.SetRequestID(value)
}

// GetType inherits docs from ActivityReply.GetType()
func (reply *ActivityHasHeartbeatDetailsReply) GetType() messagetypes.MessageType {
	return reply.ActivityReply.GetType()
}

// SetType inherits docs from ActivityReply.SetType()
func (reply *ActivityHasHeartbeatDetailsReply) SetType(value messagetypes.MessageType) {
	reply.ActivityReply.SetType(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from ActivityReply.GetError()
func (reply *ActivityHasHeartbeatDetailsReply) GetError() *cadenceerrors.CadenceError {
	return reply.ActivityReply.GetError()
}

// SetError inherits docs from ActivityReply.SetError()
func (reply *ActivityHasHeartbeatDetailsReply) SetError(value *cadenceerrors.CadenceError) {
	reply.ActivityReply.SetError(value)
}

// -------------------------------------------------------------------------
// IActivityReply interface methods for implementing the IActivityReply interface

// GetContextID inherits docs from ActivityReply.GetContextID()
func (reply *ActivityHasHeartbeatDetailsReply) GetContextID() int64 {
	return reply.ActivityReply.GetContextID()
}

// SetContextID inherits docs from ActivityReply.GetContextID()
func (reply *ActivityHasHeartbeatDetailsReply) SetContextID(value int64) {
	reply.ActivityReply.SetContextID(value)
}
