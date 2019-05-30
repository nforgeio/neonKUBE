package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// ActivityGetHeartbeatDetailsRequest is an ActivityRequest of MessageType
	// ActivityGetHeartbeatDetailsRequest.
	//
	// A ActivityGetHeartbeatDetailsRequest contains a reference to a
	// ActivityRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ActivityRequest
	//
	// Requests the details for the last heartbeat
	// recorded for a failed previous run of the activity.
	ActivityGetHeartbeatDetailsRequest struct {
		*ActivityRequest
	}
)

// NewActivityGetHeartbeatDetailsRequest is the default constructor for a ActivityGetHeartbeatDetailsRequest
//
// returns *ActivityGetHeartbeatDetailsRequest -> a pointer to a newly initialized ActivityGetHeartbeatDetailsRequest
// in memory
func NewActivityGetHeartbeatDetailsRequest() *ActivityGetHeartbeatDetailsRequest {
	request := new(ActivityGetHeartbeatDetailsRequest)
	request.ActivityRequest = NewActivityRequest()
	request.SetType(messagetypes.ActivityGetHeartbeatDetailsRequest)
	request.SetReplyType(messagetypes.ActivityGetHeartbeatDetailsReply)

	return request
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ActivityRequest.Clone()
func (request *ActivityGetHeartbeatDetailsRequest) Clone() IProxyMessage {
	activityGetHeartbeatDetailsRequest := NewActivityGetHeartbeatDetailsRequest()
	var messageClone IProxyMessage = activityGetHeartbeatDetailsRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ActivityRequest.CopyTo()
func (request *ActivityGetHeartbeatDetailsRequest) CopyTo(target IProxyMessage) {
	request.ActivityRequest.CopyTo(target)
}
