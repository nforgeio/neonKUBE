package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// ActivityHasHeartbeatDetailsRequest is an ActivityRequest of MessageType
	// ActivityHasHeartbeatDetailsRequest.
	//
	// A ActivityHasHeartbeatDetailsRequest contains a reference to a
	// ActivityRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ActivityRequest
	//
	// Determines whether a previous failed run on an
	// activity recorded heartbeat details.
	ActivityHasHeartbeatDetailsRequest struct {
		*ActivityRequest
	}
)

// NewActivityHasHeartbeatDetailsRequest is the default constructor for a ActivityHasHeartbeatDetailsRequest
//
// returns *ActivityHasHeartbeatDetailsRequest -> a pointer to a newly initialized ActivityHasHeartbeatDetailsRequest
// in memory
func NewActivityHasHeartbeatDetailsRequest() *ActivityHasHeartbeatDetailsRequest {
	request := new(ActivityHasHeartbeatDetailsRequest)
	request.ActivityRequest = NewActivityRequest()
	request.SetType(messagetypes.ActivityHasHeartbeatDetailsRequest)
	request.SetReplyType(messagetypes.ActivityHasHeartbeatDetailsReply)

	return request
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ActivityRequest.Clone()
func (request *ActivityHasHeartbeatDetailsRequest) Clone() IProxyMessage {
	activityHasHeartbeatDetailsRequest := NewActivityHasHeartbeatDetailsRequest()
	var messageClone IProxyMessage = activityHasHeartbeatDetailsRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ActivityRequest.CopyTo()
func (request *ActivityHasHeartbeatDetailsRequest) CopyTo(target IProxyMessage) {
	request.ActivityRequest.CopyTo(target)
}
