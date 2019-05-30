package messages

import (
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
