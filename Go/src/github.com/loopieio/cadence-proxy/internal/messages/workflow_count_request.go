package messages

import (
	"fmt"
	"time"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowCountRequest is WorkflowRequest of MessageType
	// WorkflowCountRequest.
	//
	// A WorkflowCountRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// A WorkflowCountRequest will pass all of the given data
	// necessary to count the number of cadence workflow instances
	WorkflowCountRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowCountRequest is the default constructor for a WorkflowCountRequest
//
// returns *WorkflowCountRequest -> a reference to a newly initialized
// WorkflowCountRequest in memory
func NewWorkflowCountRequest() *WorkflowCountRequest {
	request := new(WorkflowCountRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(messagetypes.WorkflowCountRequest)
	request.SetReplyType(messagetypes.WorkflowCountReply)

	return request
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowCountRequest) Clone() IProxyMessage {
	workflowCountRequest := NewWorkflowCountRequest()
	var messageClone IProxyMessage = workflowCountRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowCountRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowCountRequest); ok {
		fmt.Printf("TODO: JACK -- IMPLEMENT %v", v)
	}
}

// SetProxyMessage inherits docs from WorkflowRequest.SetProxyMessage()
func (request *WorkflowCountRequest) SetProxyMessage(value *ProxyMessage) {
	request.WorkflowRequest.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from WorkflowRequest.GetProxyMessage()
func (request *WorkflowCountRequest) GetProxyMessage() *ProxyMessage {
	return request.WorkflowRequest.GetProxyMessage()
}

// GetRequestID inherits docs from WorkflowRequest.GetRequestID()
func (request *WorkflowCountRequest) GetRequestID() int64 {
	return request.WorkflowRequest.GetRequestID()
}

// SetRequestID inherits docs from WorkflowRequest.SetRequestID()
func (request *WorkflowCountRequest) SetRequestID(value int64) {
	request.WorkflowRequest.SetRequestID(value)
}

// GetType inherits docs from WorkflowRequest.GetType()
func (request *WorkflowCountRequest) GetType() messagetypes.MessageType {
	return request.WorkflowRequest.GetType()
}

// SetType inherits docs from WorkflowRequest.SetType()
func (request *WorkflowCountRequest) SetType(value messagetypes.MessageType) {
	request.WorkflowRequest.SetType(value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType inherits docs from WorkflowRequest.GetReplyType()
func (request *WorkflowCountRequest) GetReplyType() messagetypes.MessageType {
	return request.WorkflowRequest.GetReplyType()
}

// SetReplyType inherits docs from WorkflowRequest.SetReplyType()
func (request *WorkflowCountRequest) SetReplyType(value messagetypes.MessageType) {
	request.WorkflowRequest.SetReplyType(value)
}

// GetTimeout inherits docs from WorkflowRequest.GetTimeout()
func (request *WorkflowCountRequest) GetTimeout() time.Duration {
	return request.WorkflowRequest.GetTimeout()
}

// SetTimeout inherits docs from WorkflowRequest.SetTimeout()
func (request *WorkflowCountRequest) SetTimeout(value time.Duration) {
	request.WorkflowRequest.SetTimeout(value)
}

// -------------------------------------------------------------------------
// IWorkflowRequest interface methods for implementing the IWorkflowRequest interface

// GetWorkflowContextID inherits docs from WorkflowRequest.GetWorkflowContextID()
func (request *WorkflowCountRequest) GetWorkflowContextID() int64 {
	return request.WorkflowRequest.GetWorkflowContextID()
}

// SetWorkflowContextID inherits docs from WorkflowRequest.GetWorkflowContextID()
func (request *WorkflowCountRequest) SetWorkflowContextID(value int64) {
	request.WorkflowRequest.SetWorkflowContextID(value)
}
