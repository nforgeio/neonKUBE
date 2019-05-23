package messages

import (
	"time"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// ActivityRequest is base type for all workflow requests
	// All workflow requests will inherit from ActivityRequest and
	// a ActivityRequest contains a ActivityContextID, which is a int64 property
	//
	// A ActivityRequest contains a reference to a
	// ProxyReply struct in memory
	ActivityRequest struct {
		*ProxyRequest
	}

	// IActivityRequest is the interface that all workflow message requests
	// implement.  It allows access to a ActivityRequest's ActivityContextID, a property
	// that all ActivityRequests share
	IActivityRequest interface {
		GetActivityContextID() int64
		SetActivityContextID(value int64)
	}
)

// NewActivityRequest is the default constructor for a ActivityRequest
//
// returns *ActivityRequest -> a pointer to a newly initialized ActivityRequest
// in memory
func NewActivityRequest() *ActivityRequest {
	request := new(ActivityRequest)
	request.ProxyRequest = NewProxyRequest()
	request.Type = messagetypes.Unspecified
	request.SetReplyType(messagetypes.Unspecified)

	return request
}

// -------------------------------------------------------------------------
// IActivityRequest interface methods for implementing the IActivityRequest interface

// GetActivityContextID gets the ContextId from a ActivityRequest's properties
// map.
//
// returns int64 -> the long representing a ActivityRequest's ContextId
func (request *ActivityRequest) GetActivityContextID() int64 {
	return request.GetLongProperty("ActivityContextId")
}

// SetActivityContextID sets the ContextId in a ActivityRequest's properties map
//
// param value int64 -> int64 value to set as the ActivityRequest's ContextId
// in its properties map
func (request *ActivityRequest) SetActivityContextID(value int64) {
	request.SetLongProperty("ActivityContextId", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (request *ActivityRequest) Clone() IProxyMessage {
	workflowContextRequest := NewActivityRequest()
	var messageClone IProxyMessage = workflowContextRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (request *ActivityRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
	if v, ok := target.(IActivityRequest); ok {
		v.SetActivityContextID(request.GetActivityContextID())
	}
}

// SetProxyMessage inherits docs from ProxyMessage.SetProxyMessage()
func (request *ActivityRequest) SetProxyMessage(value *ProxyMessage) {
	request.ProxyRequest.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ProxyMessage.GetProxyMessage()
func (request *ActivityRequest) GetProxyMessage() *ProxyMessage {
	return request.ProxyRequest.GetProxyMessage()
}

// GetRequestID inherits docs from ProxyMessage.GetRequestID()
func (request *ActivityRequest) GetRequestID() int64 {
	return request.ProxyRequest.GetRequestID()
}

// SetRequestID inherits docs from ProxyMessage.SetRequestID()
func (request *ActivityRequest) SetRequestID(value int64) {
	request.ProxyRequest.SetRequestID(value)
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
