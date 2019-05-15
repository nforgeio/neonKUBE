package types

import (
	"github.com/loopieio/cadence-proxy/internal/messages"
)

type (

	// WorkflowRegisterRequest is ProxyRequest of MessageType
	// WorkflowRegisterRequest.
	//
	// A WorkflowRegisterRequest contains a RequestId and a reference to a
	// ProxyRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ProxyRequest
	//
	// A WorkflowRegisterRequest will pass all of the given information
	// necessary to register a workflow function with the cadence server
	// via the cadence client
	WorkflowRegisterRequest struct {
		*ProxyRequest
	}
)

// NewWorkflowRegisterRequest is the default constructor for a WorkflowRegisterRequest
//
// returns *WorkflowRegisterRequest -> a reference to a newly initialized
// WorkflowRegisterRequest in memory
func NewWorkflowRegisterRequest() *WorkflowRegisterRequest {
	request := new(WorkflowRegisterRequest)
	request.ProxyRequest = NewProxyRequest()
	request.Type = messages.WorkflowRegisterRequest
	request.SetReplyType(messages.WorkflowRegisterReply)

	return request
}

// GetName gets a WorkflowRegisterRequest's Name value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowRegisterRequest's Name
func (request *WorkflowRegisterRequest) GetName() *string {
	return request.GetStringProperty("Name")
}

// SetName sets a WorkflowRegisterRequest's Name value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowRegisterRequest) SetName(value *string) {
	request.SetStringProperty("Name", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (request *WorkflowRegisterRequest) Clone() IProxyMessage {
	WorkflowRegisterRequest := NewWorkflowRegisterRequest()
	var messageClone IProxyMessage = WorkflowRegisterRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (request *WorkflowRegisterRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
	if v, ok := target.(*WorkflowRegisterRequest); ok {
		v.SetName(request.GetName())
	}
}

// SetProxyMessage inherits docs from ProxyMessage.SetProxyMessage()
func (request *WorkflowRegisterRequest) SetProxyMessage(value *ProxyMessage) {
	request.ProxyMessage.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ProxyMessage.GetProxyMessage()
func (request *WorkflowRegisterRequest) GetProxyMessage() *ProxyMessage {
	return request.ProxyMessage.GetProxyMessage()
}

// GetRequestID inherits docs from ProxyMessage.GetRequestID()
func (request *WorkflowRegisterRequest) GetRequestID() int64 {
	return request.ProxyMessage.GetRequestID()
}

// SetRequestID inherits docs from ProxyMessage.SetRequestID()
func (request *WorkflowRegisterRequest) SetRequestID(value int64) {
	request.ProxyMessage.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType inherits docs from ProxyRequest.GetReplyType()
func (request *WorkflowRegisterRequest) GetReplyType() messages.MessageType {
	return request.ProxyRequest.GetReplyType()
}

// SetReplyType inherits docs from ProxyRequest.SetReplyType()
func (request *WorkflowRegisterRequest) SetReplyType(value messages.MessageType) {
	request.ProxyRequest.SetReplyType(value)
}
