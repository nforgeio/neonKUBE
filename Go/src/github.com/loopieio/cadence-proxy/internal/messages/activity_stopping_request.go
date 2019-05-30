package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// ActivityStoppingRequest is an ActivityRequest of MessageType
	// ActivityStoppingRequest.
	//
	// A ActivityStoppingRequest contains a reference to a
	// ActivityRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ActivityRequest
	//
	// Sent to a worker, instructing it to stop executing
	// a workflow activity.
	ActivityStoppingRequest struct {
		*ActivityRequest
	}
)

// NewActivityStoppingRequest is the default constructor for a ActivityStoppingRequest
//
// returns *ActivityStoppingRequest -> a pointer to a newly initialized ActivityStoppingRequest
// in memory
func NewActivityStoppingRequest() *ActivityStoppingRequest {
	request := new(ActivityStoppingRequest)
	request.ActivityRequest = NewActivityRequest()
	request.SetType(messagetypes.ActivityStoppingRequest)
	request.SetReplyType(messagetypes.ActivityStoppingReply)

	return request
}

// GetActivityID gets a ActivityStoppingRequest's ActivityID field
// from its properties map.  Specifies the activity being stopped.
//
// returns *string -> pointer to string in memory holding
// the activityID of the activity to be stopped
func (request *ActivityStoppingRequest) GetActivityID() *string {
	return request.GetStringProperty("ActivityId")
}

// SetActivityID sets an ActivityStoppingRequest's ActivityID field
// from its properties map.  Specifies the activity being stopped.
//
// param value *string -> pointer to string in memory holding
// the activityID of the activity to be stopped
func (request *ActivityStoppingRequest) SetActivityID(value *string) {
	request.SetStringProperty("ActivityId", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ActivityRequest.Clone()
func (request *ActivityStoppingRequest) Clone() IProxyMessage {
	activityStoppingRequest := NewActivityStoppingRequest()
	var messageClone IProxyMessage = activityStoppingRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ActivityRequest.CopyTo()
func (request *ActivityStoppingRequest) CopyTo(target IProxyMessage) {
	request.ActivityRequest.CopyTo(target)
	if v, ok := target.(*ActivityStoppingRequest); ok {
		v.SetActivityID(request.GetActivityID())
	}
}
