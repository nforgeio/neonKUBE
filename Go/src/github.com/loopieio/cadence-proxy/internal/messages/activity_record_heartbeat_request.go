package messages

import (
	"time"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// ActivityRecordHeartbeatRequest is an ActivityRequest of MessageType
	// ActivityRecordHeartbeatRequest.
	//
	// A ActivityRecordHeartbeatRequest contains a reference to a
	// ActivityRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ActivityRequest
	//
	// Records an activity heartbeat.
	ActivityRecordHeartbeatRequest struct {
		*ActivityRequest
	}
)

// NewActivityRecordHeartbeatRequest is the default constructor for a ActivityRecordHeartbeatRequest
//
// returns *ActivityRecordHeartbeatRequest -> a pointer to a newly initialized ActivityRecordHeartbeatRequest
// in memory
func NewActivityRecordHeartbeatRequest() *ActivityRecordHeartbeatRequest {
	request := new(ActivityRecordHeartbeatRequest)
	request.ActivityRequest = NewActivityRequest()
	request.SetType(messagetypes.ActivityRecordHeartbeatRequest)
	request.SetReplyType(messagetypes.ActivityRecordHeartbeatReply)

	return request
}

// GetDetails gets the Activity heartbeat Details or nil
// from a ActivityRecordHeartbeatRequest's properties map.
// Returns the activity heartbeat details encoded as a byte array.
//
// returns []byte -> []byte representing the encoded activity heartbeat Details
func (request *ActivityRecordHeartbeatRequest) GetDetails() []byte {
	return request.GetBytesProperty("Details")
}

// SetDetails sets the Activity heartbeat Details or nil
// in a ActivityRecordHeartbeatRequest's properties map.
// Returns the activity heartbeat details encoded as a byte array.
//
// param value []byte -> []byte representing the encoded activity heartbeat
// Details, to be set in the ActivityRecordHeartbeatRequest's properties map
func (request *ActivityRecordHeartbeatRequest) SetDetails(value []byte) {
	request.SetBytesProperty("Details", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ActivityRequest.Clone()
func (request *ActivityRecordHeartbeatRequest) Clone() IProxyMessage {
	activityRecordHeartbeatRequest := NewActivityRecordHeartbeatRequest()
	var messageClone IProxyMessage = activityRecordHeartbeatRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ActivityRequest.CopyTo()
func (request *ActivityRecordHeartbeatRequest) CopyTo(target IProxyMessage) {
	request.ActivityRequest.CopyTo(target)
	if v, ok := target.(*ActivityRecordHeartbeatRequest); ok {
		v.SetDetails(request.GetDetails())
	}
}

// SetProxyMessage inherits docs from ActivityRequest.SetProxyMessage()
func (request *ActivityRecordHeartbeatRequest) SetProxyMessage(value *ProxyMessage) {
	request.ActivityRequest.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ActivityRequest.GetProxyMessage()
func (request *ActivityRecordHeartbeatRequest) GetProxyMessage() *ProxyMessage {
	return request.ActivityRequest.GetProxyMessage()
}

// GetRequestID inherits docs from ActivityRequest.GetRequestID()
func (request *ActivityRecordHeartbeatRequest) GetRequestID() int64 {
	return request.ActivityRequest.GetRequestID()
}

// SetRequestID inherits docs from ActivityRequest.SetRequestID()
func (request *ActivityRecordHeartbeatRequest) SetRequestID(value int64) {
	request.ActivityRequest.SetRequestID(value)
}

// GetType inherits docs from ActivityRequest.GetType()
func (request *ActivityRecordHeartbeatRequest) GetType() messagetypes.MessageType {
	return request.ActivityRequest.GetType()
}

// SetType inherits docs from ActivityRequest.SetType()
func (request *ActivityRecordHeartbeatRequest) SetType(value messagetypes.MessageType) {
	request.ActivityRequest.SetType(value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType inherits docs from ActivityRequest.GetReplyType()
func (request *ActivityRecordHeartbeatRequest) GetReplyType() messagetypes.MessageType {
	return request.ActivityRequest.GetReplyType()
}

// SetReplyType inherits docs from ActivityRequest.SetReplyType()
func (request *ActivityRecordHeartbeatRequest) SetReplyType(value messagetypes.MessageType) {
	request.ActivityRequest.SetReplyType(value)
}

// GetTimeout inherits docs from ActivityRequest.GetTimeout()
func (request *ActivityRecordHeartbeatRequest) GetTimeout() time.Duration {
	return request.ActivityRequest.GetTimeout()
}

// SetTimeout inherits docs from ActivityRequest.SetTimeout()
func (request *ActivityRecordHeartbeatRequest) SetTimeout(value time.Duration) {
	request.ActivityRequest.SetTimeout(value)
}

// -------------------------------------------------------------------------
// IActivityRequest interface methods for implementing the IActivityRequest interface

// GetContextID inherits docs from ActivityRequest.GetContextID()
func (request *ActivityRecordHeartbeatRequest) GetContextID() int64 {
	return request.ActivityRequest.GetContextID()
}

// SetContextID inherits docs from ActivityRequest.SetContextID()
func (request *ActivityRecordHeartbeatRequest) SetContextID(value int64) {
	request.ActivityRequest.SetContextID(value)
}
