package messages

import (
	"time"

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

// SetProxyMessage inherits docs from ActivityRequest.SetProxyMessage()
func (request *ActivityStoppingRequest) SetProxyMessage(value *ProxyMessage) {
	request.ActivityRequest.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ActivityRequest.GetProxyMessage()
func (request *ActivityStoppingRequest) GetProxyMessage() *ProxyMessage {
	return request.ActivityRequest.GetProxyMessage()
}

// GetRequestID inherits docs from ActivityRequest.GetRequestID()
func (request *ActivityStoppingRequest) GetRequestID() int64 {
	return request.ActivityRequest.GetRequestID()
}

// SetRequestID inherits docs from ActivityRequest.SetRequestID()
func (request *ActivityStoppingRequest) SetRequestID(value int64) {
	request.ActivityRequest.SetRequestID(value)
}

// GetType inherits docs from ActivityRequest.GetType()
func (request *ActivityStoppingRequest) GetType() messagetypes.MessageType {
	return request.ActivityRequest.GetType()
}

// SetType inherits docs from ActivityRequest.SetType()
func (request *ActivityStoppingRequest) SetType(value messagetypes.MessageType) {
	request.ActivityRequest.SetType(value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType inherits docs from ActivityRequest.GetReplyType()
func (request *ActivityStoppingRequest) GetReplyType() messagetypes.MessageType {
	return request.ActivityRequest.GetReplyType()
}

// SetReplyType inherits docs from ActivityRequest.SetReplyType()
func (request *ActivityStoppingRequest) SetReplyType(value messagetypes.MessageType) {
	request.ActivityRequest.SetReplyType(value)
}

// GetTimeout inherits docs from ActivityRequest.GetTimeout()
func (request *ActivityStoppingRequest) GetTimeout() time.Duration {
	return request.ActivityRequest.GetTimeout()
}

// SetTimeout inherits docs from ActivityRequest.SetTimeout()
func (request *ActivityStoppingRequest) SetTimeout(value time.Duration) {
	request.ActivityRequest.SetTimeout(value)
}

// -------------------------------------------------------------------------
// IActivityRequest interface methods for implementing the IActivityRequest interface

// GetContextID inherits docs from ActivityRequest.GetContextID()
func (request *ActivityStoppingRequest) GetContextID() int64 {
	return request.ActivityRequest.GetContextID()
}

// SetContextID inherits docs from ActivityRequest.SetContextID()
func (request *ActivityStoppingRequest) SetContextID(value int64) {
	request.ActivityRequest.SetContextID(value)
}
