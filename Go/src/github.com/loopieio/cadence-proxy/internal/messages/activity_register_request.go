package messages

import (
	"time"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// ActivityRegisterRequest is an ActivityRequest of MessageType
	// ActivityRegisterRequest.
	//
	// A ActivityRegisterRequest contains a reference to a
	// ActivityRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ActivityRequest
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
	request.Type = messagetypes.ActivityRegisterRequest
	request.SetReplyType(messagetypes.ActivityRegisterReply)

	return request
}

// GetName gets a ActivityRegisterRequest's Name value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a ActivityRegisterRequest's Name
func (request *ActivityRegisterRequest) GetName() *string {
	return request.GetStringProperty("Name")
}

// SetName sets a ActivityRegisterRequest's Name value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *ActivityRegisterRequest) SetName(value *string) {
	request.SetStringProperty("Name", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (request *ActivityRegisterRequest) Clone() IProxyMessage {
	activityRegisterRequest := NewActivityRegisterRequest()
	var messageClone IProxyMessage = activityRegisterRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (request *ActivityRegisterRequest) CopyTo(target IProxyMessage) {
	request.ActivityRequest.CopyTo(target)
	if v, ok := target.(*ActivityRegisterRequest); ok {
		v.SetActivityContextID(request.GetActivityContextID())
		v.SetName(request.GetName())
	}
}

// SetProxyMessage inherits docs from ProxyMessage.SetProxyMessage()
func (request *ActivityRegisterRequest) SetProxyMessage(value *ProxyMessage) {
	request.ActivityRequest.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ProxyMessage.GetProxyMessage()
func (request *ActivityRegisterRequest) GetProxyMessage() *ProxyMessage {
	return request.ActivityRequest.GetProxyMessage()
}

// GetRequestID inherits docs from ProxyMessage.GetRequestID()
func (request *ActivityRegisterRequest) GetRequestID() int64 {
	return request.ActivityRequest.GetRequestID()
}

// SetRequestID inherits docs from ProxyMessage.SetRequestID()
func (request *ActivityRegisterRequest) SetRequestID(value int64) {
	request.ActivityRequest.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IActivityRequest interface methods for implementing the IActivityRequest interface

// GetReplyType inherits docs from ActivityRequest.GetReplyType()
func (request *ActivityRegisterRequest) GetReplyType() messagetypes.MessageType {
	return request.ActivityRequest.GetReplyType()
}

// SetReplyType inherits docs from ActivityRequest.SetReplyType()
func (request *ActivityRegisterRequest) SetReplyType(value messagetypes.MessageType) {
	request.ActivityRequest.SetReplyType(value)
}

// GetTimeout inherits docs from ActivityRequest.GetTimeout()
func (request *ActivityRegisterRequest) GetTimeout() time.Duration {
	return request.ActivityRequest.GetTimeout()
}

// SetTimeout inherits docs from ActivityRequest.SetTimeout()
func (request *ActivityRegisterRequest) SetTimeout(value time.Duration) {
	request.ActivityRequest.SetTimeout(value)
}

// -------------------------------------------------------------------------
// IActivityRequest interface methods for implementing the IActivityRequest interface

// GetActivityContextID inherits docs from ActivityRequest.GetActivityContextID()
func (request *ActivityRegisterRequest) GetActivityContextID() int64 {
	return request.ActivityRequest.GetActivityContextID()
}

// SetActivityContextID inherits docs from ActivityRequest.SetActivityContextID()
func (request *ActivityRegisterRequest) SetActivityContextID(value int64) {
	request.ActivityRequest.SetActivityContextID(value)
}
