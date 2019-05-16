package messages

import (
	"time"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowContextRequest is base type for all workflow requests
	// All workflow requests will inherit from WorkflowContextRequest and
	// a WorkflowContextRequest contains a ContextID, which is a int64 property
	//
	// A WorkflowContextRequest contains a reference to a
	// ProxyReply struct in memory
	WorkflowContextRequest struct {
		*ProxyRequest
	}

	// IWorkflowContextRequest is the interface that all workflow message requests
	// implement.  It allows access to a WorkflowContextRequest's ContextID, a property
	// that all WorkflowRequests share
	IWorkflowContextRequest interface {
		GetContextID() int64
		SetContextID(value int64)
	}
)

// NewWorkflowContextRequest is the default constructor for a WorkflowContextRequest
//
// returns *WorkflowContextRequest -> a pointer to a newly initialized WorkflowContextRequest
// in memory
func NewWorkflowContextRequest() *WorkflowContextRequest {
	request := new(WorkflowContextRequest)
	request.ProxyRequest = NewProxyRequest()
	request.Type = messagetypes.Unspecified
	request.SetReplyType(messagetypes.Unspecified)

	return request
}

// -------------------------------------------------------------------------
// IWorkflowContextRequest interface methods for implementing the IWorkflowContextRequest interface

// GetContextID gets the ContextId from a WorkflowContextRequest's properties
// map.
//
// returns int64 -> the long representing a WorkflowContextRequest's ContextId
func (request *WorkflowContextRequest) GetContextID() int64 {
	return request.GetLongProperty("WorkflowContextId")
}

// SetContextID sets the ContextId in a WorkflowContextRequest's properties map
//
// param value int64 -> int64 value to set as the WorkflowContextRequest's ContextId
// in its properties map
func (request *WorkflowContextRequest) SetContextID(value int64) {
	request.SetLongProperty("WorkflowContextId", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (request *WorkflowContextRequest) Clone() IProxyMessage {
	workflowContextRequest := NewWorkflowContextRequest()
	var messageClone IProxyMessage = workflowContextRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (request *WorkflowContextRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
	if v, ok := target.(IWorkflowContextRequest); ok {
		v.SetContextID(request.GetContextID())
	}
}

// SetProxyMessage inherits docs from ProxyMessage.SetProxyMessage()
func (request *WorkflowContextRequest) SetProxyMessage(value *ProxyMessage) {
	request.ProxyRequest.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ProxyMessage.GetProxyMessage()
func (request *WorkflowContextRequest) GetProxyMessage() *ProxyMessage {
	return request.ProxyRequest.GetProxyMessage()
}

// GetRequestID inherits docs from ProxyMessage.GetRequestID()
func (request *WorkflowContextRequest) GetRequestID() int64 {
	return request.ProxyRequest.GetRequestID()
}

// SetRequestID inherits docs from ProxyMessage.SetRequestID()
func (request *WorkflowContextRequest) SetRequestID(value int64) {
	request.ProxyRequest.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType inherits docs from ProxyRequest.GetReplyType()
func (request *WorkflowContextRequest) GetReplyType() messagetypes.MessageType {
	return request.ProxyRequest.GetReplyType()
}

// SetReplyType inherits docs from ProxyRequest.SetReplyType()
func (request *WorkflowContextRequest) SetReplyType(value messagetypes.MessageType) {
	request.ProxyRequest.SetReplyType(value)
}

// GetTimeout inherits docs from ProxyRequest.GetTimeout()
func (request *WorkflowContextRequest) GetTimeout() time.Duration {
	return request.ProxyRequest.GetTimeout()
}

// SetTimeout inherits docs from ProxyRequest.SetTimeout()
func (request *WorkflowContextRequest) SetTimeout(value time.Duration) {
	request.ProxyRequest.SetTimeout(value)
}
