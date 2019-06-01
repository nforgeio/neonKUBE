package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// ActivityRegisterRequest is an ActivityRequest of MessageType
	// ActivityRegisterRequest.
	//
	// A ActivityRegisterRequest contains a reference to a
	// ActivityRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ActivityRequest
	//
	// Registers an activity with the cadence server
	ActivityRegisterRequest struct {
		*ActivityRequest
	}
)

// NewActivityRegisterRequest is the default constructor for a ActivityRegisterRequest
//
// returns *ActivityRegisterRequest -> a pointer to a newly initialized ActivityRegisterRequest
// in memory
func NewActivityRegisterRequest() *ActivityRegisterRequest {
	request := new(ActivityRegisterRequest)
	request.ActivityRequest = NewActivityRequest()
	request.SetType(messagetypes.ActivityRegisterRequest)
	request.SetReplyType(messagetypes.ActivityRegisterReply)

	return request
}

// GetName gets a ActivityRegisterRequest's Name field
// from its properties map.  Specifies the name of the activity to
// be registered.
//
// returns *string -> *string representing the name of the
// activity to be registered
func (request *ActivityRegisterRequest) GetName() *string {
	return request.GetStringProperty("Name")
}

// SetName sets an ActivityRegisterRequest's Name field
// from its properties map.  Specifies the name of the activity to
// be registered.
//
// param value *string -> *string representing the name of the
// activity to be registered
func (request *ActivityRegisterRequest) SetName(value *string) {
	request.SetStringProperty("Name", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ActivityRequest.Clone()
func (request *ActivityRegisterRequest) Clone() IProxyMessage {
	activityRegisterRequest := NewActivityRegisterRequest()
	var messageClone IProxyMessage = activityRegisterRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ActivityRequest.CopyTo()
func (request *ActivityRegisterRequest) CopyTo(target IProxyMessage) {
	request.ActivityRequest.CopyTo(target)
	if v, ok := target.(*ActivityRegisterRequest); ok {
		v.SetName(request.GetName())
	}
}
