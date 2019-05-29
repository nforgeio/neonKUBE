package messages

import (
	"time"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// ActivityRequest is base type for all workflow requests
	// All workflow requests will inherit from ActivityRequest and
	// a ActivityRequest contains a ContextID, which is a int64 property
	//
	// A ActivityRequest contains a reference to a
	// ProxyReply struct in memory
	ActivityRequest struct {
		*ProxyRequest
	}

	// IActivityRequest is the interface that all workflow message requests
	// implement.  It allows access to a ActivityRequest's ContextID, a property
	// that all ActivityRequests share
	IActivityRequest interface {
		GetContextID() int64
		SetContextID(value int64)
		GetReplyType() messagetypes.MessageType
		SetReplyType(value messagetypes.MessageType)
		GetTimeout() time.Duration
		SetTimeout(value time.Duration)
		Clone() IProxyMessage
		CopyTo(target IProxyMessage)
		SetProxyMessage(value *ProxyMessage)
		GetProxyMessage() *ProxyMessage
		GetRequestID() int64
		SetRequestID(int64)
		GetType() messagetypes.MessageType
		SetType(value messagetypes.MessageType)
	}
)

// NewActivityRequest is the default constructor for a ActivityRequest
//
// returns *ActivityRequest -> a pointer to a newly initialized ActivityRequest
// in memory
func NewActivityRequest() *ActivityRequest {
	request := new(ActivityRequest)
	request.ProxyRequest = NewProxyRequest()
	request.SetType(messagetypes.Unspecified)
	request.SetReplyType(messagetypes.Unspecified)

	return request
}

// -------------------------------------------------------------------------
// IActivityRequest interface methods for implementing the IActivityRequest interface

// GetContextID gets the ContextId from a ActivityRequest's properties
// map.
//
// returns int64 -> the long representing a ActivityRequest's ContextId
func (request *ActivityRequest) GetContextID() int64 {
	return request.GetLongProperty("ContextID")
}

// SetContextID sets the ContextId in a ActivityRequest's properties map
//
// param value int64 -> int64 value to set as the ActivityRequest's ContextId
// in its properties map
func (request *ActivityRequest) SetContextID(value int64) {
	request.SetLongProperty("ContextID", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyRequest.Clone()
func (request *ActivityRequest) Clone() IProxyMessage {
	workflowContextRequest := NewActivityRequest()
	var messageClone IProxyMessage = workflowContextRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyRequest.CopyTo()
func (request *ActivityRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
	if v, ok := target.(IActivityRequest); ok {
		v.SetContextID(request.GetContextID())
	}
}

// SetProxyMessage inherits docs from ProxyRequest.SetProxyMessage()
func (request *ActivityRequest) SetProxyMessage(value *ProxyMessage) {
	request.ProxyRequest.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ProxyRequest.GetProxyMessage()
func (request *ActivityRequest) GetProxyMessage() *ProxyMessage {
	return request.ProxyRequest.GetProxyMessage()
}

// GetRequestID inherits docs from ProxyRequest.GetRequestID()
func (request *ActivityRequest) GetRequestID() int64 {
	return request.ProxyRequest.GetRequestID()
}

// SetRequestID inherits docs from ProxyRequest.SetRequestID()
func (request *ActivityRequest) SetRequestID(value int64) {
	request.ProxyRequest.SetRequestID(value)
}

// GetType inherits docs from ProxyRequest.GetType()
func (request *ActivityRequest) GetType() messagetypes.MessageType {
	return request.ProxyRequest.GetType()
}

// SetType inherits docs from ProxyRequest.SetType()
func (request *ActivityRequest) SetType(value messagetypes.MessageType) {
	request.ProxyRequest.SetType(value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType inherits docs from ProxyRequest.GetReplyType()
func (request *ActivityRequest) GetReplyType() messagetypes.MessageType {
	return request.ProxyRequest.GetReplyType()
}

// SetReplyType inherits docs from ProxyRequest.SetReplyType()
func (request *ActivityRequest) SetReplyType(value messagetypes.MessageType) {
	request.ProxyRequest.SetReplyType(value)
}

// GetTimeout inherits docs from ProxyRequest.GetTimeout()
func (request *ActivityRequest) GetTimeout() time.Duration {
	return request.ProxyRequest.GetTimeout()
}

// SetTimeout inherits docs from ProxyRequest.SetTimeout()
func (request *ActivityRequest) SetTimeout(value time.Duration) {
	request.ProxyRequest.SetTimeout(value)
}
