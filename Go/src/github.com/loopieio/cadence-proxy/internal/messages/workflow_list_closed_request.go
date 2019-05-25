package messages

import (
	"fmt"
	"time"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowListClosedRequest is WorkflowRequest of MessageType
	// WorkflowListClosedRequest.
	//
	// A WorkflowListClosedRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// A WorkflowListClosedRequest will pass all of the given data
	// necessary to list the closed cadence workflow execution instances
	WorkflowListClosedRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowListClosedRequest is the default constructor for a WorkflowListClosedRequest
//
// returns *WorkflowListClosedRequest -> a reference to a newly initialized
// WorkflowListClosedRequest in memory
func NewWorkflowListClosedRequest() *WorkflowListClosedRequest {
	request := new(WorkflowListClosedRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(messagetypes.WorkflowListClosedRequest)
	request.SetReplyType(messagetypes.WorkflowListClosedReply)

	return request
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowListClosedRequest) Clone() IProxyMessage {
	WorkflowListClosedRequest := NewWorkflowListClosedRequest()
	var messageClone IProxyMessage = WorkflowListClosedRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowListClosedRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowListClosedRequest); ok {
		fmt.Printf("TODO: JACK -- IMPLEMENT %v", v)
	}
}

// SetProxyMessage inherits docs from WorkflowRequest.SetProxyMessage()
func (request *WorkflowListClosedRequest) SetProxyMessage(value *ProxyMessage) {
	request.WorkflowRequest.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from WorkflowRequest.GetProxyMessage()
func (request *WorkflowListClosedRequest) GetProxyMessage() *ProxyMessage {
	return request.WorkflowRequest.GetProxyMessage()
}

// GetRequestID inherits docs from WorkflowRequest.GetRequestID()
func (request *WorkflowListClosedRequest) GetRequestID() int64 {
	return request.WorkflowRequest.GetRequestID()
}

// SetRequestID inherits docs from WorkflowRequest.SetRequestID()
func (request *WorkflowListClosedRequest) SetRequestID(value int64) {
	request.WorkflowRequest.SetRequestID(value)
}

// GetType inherits docs from WorkflowRequest.GetType()
func (request *WorkflowListClosedRequest) GetType() messagetypes.MessageType {
	return request.WorkflowRequest.GetType()
}

// SetType inherits docs from WorkflowRequest.SetType()
func (request *WorkflowListClosedRequest) SetType(value messagetypes.MessageType) {
	request.WorkflowRequest.SetType(value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType inherits docs from WorkflowRequest.GetReplyType()
func (request *WorkflowListClosedRequest) GetReplyType() messagetypes.MessageType {
	return request.WorkflowRequest.GetReplyType()
}

// SetReplyType inherits docs from WorkflowRequest.SetReplyType()
func (request *WorkflowListClosedRequest) SetReplyType(value messagetypes.MessageType) {
	request.WorkflowRequest.SetReplyType(value)
}

// GetTimeout inherits docs from WorkflowRequest.GetTimeout()
func (request *WorkflowListClosedRequest) GetTimeout() time.Duration {
	return request.WorkflowRequest.GetTimeout()
}

// SetTimeout inherits docs from WorkflowRequest.SetTimeout()
func (request *WorkflowListClosedRequest) SetTimeout(value time.Duration) {
	request.WorkflowRequest.SetTimeout(value)
}

// -------------------------------------------------------------------------
// IWorkflowRequest interface methods for implementing the IWorkflowRequest interface

// GetWorkflowContextID inherits docs from WorkflowRequest.GetWorkflowContextID()
func (request *WorkflowListClosedRequest) GetWorkflowContextID() int64 {
	return request.WorkflowRequest.GetWorkflowContextID()
}

// SetWorkflowContextID inherits docs from WorkflowRequest.GetWorkflowContextID()
func (request *WorkflowListClosedRequest) SetWorkflowContextID(value int64) {
	request.WorkflowRequest.SetWorkflowContextID(value)
}
