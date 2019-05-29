package messages

import (
	"time"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// ActivityInvokeRequest is an ActivityRequest of MessageType
	// ActivityInvokeRequest.
	//
	// A ActivityInvokeRequest contains a reference to a
	// ActivityRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ActivityRequest
	//
	// Sent to a worker, instructing it to begin executing
	// a workflow activity.
	ActivityInvokeRequest struct {
		*ActivityRequest
	}
)

// NewActivityInvokeRequest is the default constructor for a ActivityInvokeRequest
//
// returns *ActivityInvokeRequest -> a pointer to a newly initialized ActivityInvokeRequest
// in memory
func NewActivityInvokeRequest() *ActivityInvokeRequest {
	request := new(ActivityInvokeRequest)
	request.ActivityRequest = NewActivityRequest()
	request.SetType(messagetypes.ActivityInvokeRequest)
	request.SetReplyType(messagetypes.ActivityInvokeReply)

	return request
}

// GetArgs gets a ActivityInvokeRequest's Args field
// from its properties map.  Optionally specifies the activity
// arguments encoded as a byte array.
//
// returns []byte -> []byte representing workflow activity parameters or arguments
func (request *ActivityInvokeRequest) GetArgs() []byte {
	return request.GetBytesProperty("Args")
}

// SetArgs sets an ActivityInvokeRequest's Args field
// from its properties map.  Optionally specifies the activity
// arguments encoded as a byte array.
//
// param value []byte -> []byte representing workflow activity parameters or arguments
func (request *ActivityInvokeRequest) SetArgs(value []byte) {
	request.SetBytesProperty("Args", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ActivityRequest.Clone()
func (request *ActivityInvokeRequest) Clone() IProxyMessage {
	activityInvokeRequest := NewActivityInvokeRequest()
	var messageClone IProxyMessage = activityInvokeRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ActivityRequest.CopyTo()
func (request *ActivityInvokeRequest) CopyTo(target IProxyMessage) {
	request.ActivityRequest.CopyTo(target)
	if v, ok := target.(*ActivityInvokeRequest); ok {
		v.SetArgs(request.GetArgs())
	}
}

// SetProxyMessage inherits docs from ActivityRequest.SetProxyMessage()
func (request *ActivityInvokeRequest) SetProxyMessage(value *ProxyMessage) {
	request.ActivityRequest.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ActivityRequest.GetProxyMessage()
func (request *ActivityInvokeRequest) GetProxyMessage() *ProxyMessage {
	return request.ActivityRequest.GetProxyMessage()
}

// GetRequestID inherits docs from ActivityRequest.GetRequestID()
func (request *ActivityInvokeRequest) GetRequestID() int64 {
	return request.ActivityRequest.GetRequestID()
}

// SetRequestID inherits docs from ActivityRequest.SetRequestID()
func (request *ActivityInvokeRequest) SetRequestID(value int64) {
	request.ActivityRequest.SetRequestID(value)
}

// GetType inherits docs from ActivityRequest.GetType()
func (request *ActivityInvokeRequest) GetType() messagetypes.MessageType {
	return request.ActivityRequest.GetType()
}

// SetType inherits docs from ActivityRequest.SetType()
func (request *ActivityInvokeRequest) SetType(value messagetypes.MessageType) {
	request.ActivityRequest.SetType(value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType inherits docs from ActivityRequest.GetReplyType()
func (request *ActivityInvokeRequest) GetReplyType() messagetypes.MessageType {
	return request.ActivityRequest.GetReplyType()
}

// SetReplyType inherits docs from ActivityRequest.SetReplyType()
func (request *ActivityInvokeRequest) SetReplyType(value messagetypes.MessageType) {
	request.ActivityRequest.SetReplyType(value)
}

// GetTimeout inherits docs from ActivityRequest.GetTimeout()
func (request *ActivityInvokeRequest) GetTimeout() time.Duration {
	return request.ActivityRequest.GetTimeout()
}

// SetTimeout inherits docs from ActivityRequest.SetTimeout()
func (request *ActivityInvokeRequest) SetTimeout(value time.Duration) {
	request.ActivityRequest.SetTimeout(value)
}

// -------------------------------------------------------------------------
// IActivityRequest interface methods for implementing the IActivityRequest interface

// GetContextID inherits docs from ActivityRequest.GetContextID()
func (request *ActivityInvokeRequest) GetContextID() int64 {
	return request.ActivityRequest.GetContextID()
}

// SetContextID inherits docs from ActivityRequest.SetContextID()
func (request *ActivityInvokeRequest) SetContextID(value int64) {
	request.ActivityRequest.SetContextID(value)
}
