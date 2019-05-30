package messages

import (
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
		IProxyRequest
		GetContextID() int64
		SetContextID(value int64)
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
