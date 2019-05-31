package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// ActivityInvokeLocalRequest is an ActivityRequest of MessageType
	// ActivityInvokeLocalRequest.
	//
	// A ActivityInvokeLocalRequest contains a reference to a
	// ActivityRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ActivityRequest
	//
	// Sent to a worker, instructing it to begin executing
	// a local workflow activity.
	ActivityInvokeLocalRequest struct {
		*ActivityRequest
	}
)

// NewActivityInvokeLocalRequest is the default constructor for a ActivityInvokeLocalRequest
//
// returns *ActivityInvokeLocalRequest -> a pointer to a newly initialized ActivityInvokeLocalRequest
// in memory
func NewActivityInvokeLocalRequest() *ActivityInvokeLocalRequest {
	request := new(ActivityInvokeLocalRequest)
	request.ActivityRequest = NewActivityRequest()
	request.SetType(messagetypes.ActivityInvokeLocalRequest)
	request.SetReplyType(messagetypes.ActivityInvokeLocalReply)

	return request
}

// GetActivityContextID gets a ActivityInvokeLocalRequest's ActivityContextID field
// from its properties map.  Identifies the activity context.
//
// returns int64 -> int64 representing the ActivityContextID of the
// activity to be executed
func (request *ActivityInvokeLocalRequest) GetActivityContextID() int64 {
	return request.GetLongProperty("ActivityContextId")
}

// SetActivity sets an ActivityInvokeLocalRequest's ActivityContextID field
// from its properties map.  Identifies the activity context.
//
// param value int64 -> int64 representing the ActivityContextID of the
// activity to be executed
func (request *ActivityInvokeLocalRequest) SetActivityContextID(value int64) {
	request.SetLongProperty("ActivityContextId", value)
}

// GetActivityTypeID gets a ActivityInvokeLocalRequest's ActivityTypeID field
// from its properties map.  Identifies the .NET type that
// implements the local activity.
//
// returns int64 -> int64 representing the ActivityTypeID of the
// activity to be executed
func (request *ActivityInvokeLocalRequest) GetActivityTypeID() int64 {
	return request.GetLongProperty("ActivityTypeId")
}

// SetActivity sets an ActivityInvokeLocalRequest's ActivityTypeID field
// from its properties map.  Identifies the .NET type that
// implements the local activity.
//
// param value int64 -> int64 representing the ActivityTypeID of the
// activity to be executed
func (request *ActivityInvokeLocalRequest) SetActivityTypeID(value int64) {
	request.SetLongProperty("ActivityTypeId", value)
}

// GetArgs gets a ActivityInvokeLocalRequest's Args field
// from its properties map.  Optionally specifies the
// arguments to be passed to the activity encoded as a byte array.
//
// returns []byte -> []byte representing workflow activity parameters or arguments
// for executing
func (request *ActivityInvokeLocalRequest) GetArgs() []byte {
	return request.GetBytesProperty("Args")
}

// SetArgs sets an ActivityInvokeLocalRequest's Args field
// from its properties map.  Optionally specifies the
// arguments to be passed to the activity encoded as a byte array.
//
// param value []byte -> []byte representing workflow activity parameters or arguments
// for executing
func (request *ActivityInvokeLocalRequest) SetArgs(value []byte) {
	request.SetBytesProperty("Args", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ActivityRequest.Clone()
func (request *ActivityInvokeLocalRequest) Clone() IProxyMessage {
	activityInvokeLocalRequest := NewActivityInvokeLocalRequest()
	var messageClone IProxyMessage = activityInvokeLocalRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ActivityRequest.CopyTo()
func (request *ActivityInvokeLocalRequest) CopyTo(target IProxyMessage) {
	request.ActivityRequest.CopyTo(target)
	if v, ok := target.(*ActivityInvokeLocalRequest); ok {
		v.SetActivityContextID(request.GetActivityContextID())
		v.SetArgs(request.GetArgs())
		v.SetActivityTypeID(request.GetActivityTypeID())
	}
}
