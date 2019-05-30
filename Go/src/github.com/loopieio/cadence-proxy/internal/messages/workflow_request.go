package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowRequest is base type for all workflow requests
	// All workflow requests will inherit from WorkflowRequest and
	// a WorkflowRequest contains a ContextID, which is a int64 property
	//
	// A WorkflowRequest contains a reference to a
	// ProxyReply struct in memory
	WorkflowRequest struct {
		*ProxyRequest
	}

	// IWorkflowRequest is the interface that all workflow message requests
	// implement.  It allows access to a WorkflowRequest's ContextID, a property
	// that all WorkflowRequests share
	IWorkflowRequest interface {
		IProxyRequest
		GetContextID() int64
		SetContextID(value int64)
	}
)

// NewWorkflowRequest is the default constructor for a WorkflowRequest
//
// returns *WorkflowRequest -> a pointer to a newly initialized WorkflowRequest
// in memory
func NewWorkflowRequest() *WorkflowRequest {
	request := new(WorkflowRequest)
	request.ProxyRequest = NewProxyRequest()
	request.SetType(messagetypes.Unspecified)
	request.SetReplyType(messagetypes.Unspecified)

	return request
}

// -------------------------------------------------------------------------
// IWorkflowRequest interface methods for implementing the IWorkflowRequest interface

// GetContextID gets the ContextId from a WorkflowRequest's properties
// map.
//
// returns int64 -> the long representing a WorkflowRequest's ContextId
func (request *WorkflowRequest) GetContextID() int64 {
	return request.GetLongProperty("ContextId")
}

// SetContextID sets the ContextId in a WorkflowRequest's properties map
//
// param value int64 -> int64 value to set as the WorkflowRequest's ContextId
// in its properties map
func (request *WorkflowRequest) SetContextID(value int64) {
	request.SetLongProperty("ContextId", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (request *WorkflowRequest) Clone() IProxyMessage {
	workflowContextRequest := NewWorkflowRequest()
	var messageClone IProxyMessage = workflowContextRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (request *WorkflowRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
	if v, ok := target.(IWorkflowRequest); ok {
		v.SetContextID(request.GetContextID())
	}
}
