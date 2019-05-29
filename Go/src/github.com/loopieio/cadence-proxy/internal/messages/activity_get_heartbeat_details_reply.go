package messages

import (
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// ActivityGetHeartbeatDetailsReply is a ActivityReply of MessageType
	// ActivityGetHeartbeatDetailsReply.  It holds a reference to a ActivityReply in memory
	// and is the reply type to a ActivityGetHeartbeatDetailsRequest
	ActivityGetHeartbeatDetailsReply struct {
		*ActivityReply
	}
)

// NewActivityGetHeartbeatDetailsReply is the default constructor for
// a ActivityGetHeartbeatDetailsReply
//
// returns *ActivityGetHeartbeatDetailsReply -> a pointer to a newly initialized
// ActivityGetHeartbeatDetailsReply in memory
func NewActivityGetHeartbeatDetailsReply() *ActivityGetHeartbeatDetailsReply {
	reply := new(ActivityGetHeartbeatDetailsReply)
	reply.ActivityReply = NewActivityReply()
	reply.SetType(messagetypes.ActivityGetHeartbeatDetailsReply)

	return reply
}

// GetDetails gets the Activity heartbeat Details or nil
// from a ActivityGetHeartbeatDetailsReply's properties map.
// Returns the activity heartbeat details encoded as a byte array.
//
// returns []byte -> []byte representing the encoded activity heartbeat Details
func (reply *ActivityGetHeartbeatDetailsReply) GetDetails() []byte {
	return reply.GetBytesProperty("Details")
}

// SetDetails sets the Activity heartbeat Details or nil
// in a ActivityGetHeartbeatDetailsReply's properties map.
// Returns the activity heartbeat details encoded as a byte array.
//
// param value []byte -> []byte representing the encoded activity heartbeat
// Details, to be set in the ActivityGetHeartbeatDetailsReply's properties map
func (reply *ActivityGetHeartbeatDetailsReply) SetDetails(value []byte) {
	reply.SetBytesProperty("Details", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (reply *ActivityGetHeartbeatDetailsReply) Clone() IProxyMessage {
	activityGetHeartbeatDetailsReply := NewActivityGetHeartbeatDetailsReply()
	var messageClone IProxyMessage = activityGetHeartbeatDetailsReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *ActivityGetHeartbeatDetailsReply) CopyTo(target IProxyMessage) {
	reply.ActivityReply.CopyTo(target)
	if v, ok := target.(*ActivityGetHeartbeatDetailsReply); ok {
		v.SetDetails(reply.GetDetails())
	}
}

// SetProxyMessage inherits docs from ActivityReply.SetProxyMessage()
func (reply *ActivityGetHeartbeatDetailsReply) SetProxyMessage(value *ProxyMessage) {
	reply.ActivityReply.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ActivityReply.GetProxyMessage()
func (reply *ActivityGetHeartbeatDetailsReply) GetProxyMessage() *ProxyMessage {
	return reply.ActivityReply.GetProxyMessage()
}

// GetRequestID inherits docs from ActivityReply.GetRequestID()
func (reply *ActivityGetHeartbeatDetailsReply) GetRequestID() int64 {
	return reply.ActivityReply.GetRequestID()
}

// SetRequestID inherits docs from ActivityReply.SetRequestID()
func (reply *ActivityGetHeartbeatDetailsReply) SetRequestID(value int64) {
	reply.ActivityReply.SetRequestID(value)
}

// GetType inherits docs from ActivityReply.GetType()
func (reply *ActivityGetHeartbeatDetailsReply) GetType() messagetypes.MessageType {
	return reply.ActivityReply.GetType()
}

// SetType inherits docs from ActivityReply.SetType()
func (reply *ActivityGetHeartbeatDetailsReply) SetType(value messagetypes.MessageType) {
	reply.ActivityReply.SetType(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from ActivityReply.GetError()
func (reply *ActivityGetHeartbeatDetailsReply) GetError() *cadenceerrors.CadenceError {
	return reply.ActivityReply.GetError()
}

// SetError inherits docs from ActivityReply.SetError()
func (reply *ActivityGetHeartbeatDetailsReply) SetError(value *cadenceerrors.CadenceError) {
	reply.ActivityReply.SetError(value)
}

// -------------------------------------------------------------------------
// IActivityReply interface methods for implementing the IActivityReply interface

// GetContextID inherits docs from ActivityReply.GetContextID()
func (reply *ActivityGetHeartbeatDetailsReply) GetContextID() int64 {
	return reply.ActivityReply.GetContextID()
}

// SetContextID inherits docs from ActivityReply.GetContextID()
func (reply *ActivityGetHeartbeatDetailsReply) SetContextID(value int64) {
	reply.ActivityReply.SetContextID(value)
}
