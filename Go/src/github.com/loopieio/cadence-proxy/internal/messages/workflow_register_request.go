package messages

import (
	"time"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowRegisterRequest is ProxyRequest of MessageType
	// WorkflowRegisterRequest.
	//
	// A WorkflowRegisterRequest contains a reference to a
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
	request.Type = messagetypes.WorkflowRegisterRequest
	request.SetReplyType(messagetypes.WorkflowRegisterReply)

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

// Clone inherits docs from ProxyRequest.Clone()
func (request *WorkflowRegisterRequest) Clone() IProxyMessage {
	workflowRegisterRequest := NewWorkflowRegisterRequest()
	var messageClone IProxyMessage = workflowRegisterRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyRequest.CopyTo()
func (request *WorkflowRegisterRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
	if v, ok := target.(*WorkflowRegisterRequest); ok {
		v.SetName(request.GetName())
	}
}

// SetProxyMessage inherits docs from ProxyRequest.SetProxyMessage()
func (request *WorkflowRegisterRequest) SetProxyMessage(value *ProxyMessage) {
	request.ProxyRequest.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ProxyRequest.GetProxyMessage()
func (request *WorkflowRegisterRequest) GetProxyMessage() *ProxyMessage {
	return request.ProxyRequest.GetProxyMessage()
}

// GetRequestID inherits docs from ProxyRequest.GetRequestID()
func (request *WorkflowRegisterRequest) GetRequestID() int64 {
	return request.ProxyRequest.GetRequestID()
}

// SetRequestID inherits docs from ProxyRequest.SetRequestID()
func (request *WorkflowRegisterRequest) SetRequestID(value int64) {
	request.ProxyRequest.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType inherits docs from ProxyRequest.GetReplyType()
func (request *WorkflowRegisterRequest) GetReplyType() messagetypes.MessageType {
	return request.ProxyRequest.GetReplyType()
}

// SetReplyType inherits docs from ProxyRequest.SetReplyType()
func (request *WorkflowRegisterRequest) SetReplyType(value messagetypes.MessageType) {
	request.ProxyRequest.SetReplyType(value)
}

// GetTimeout inherits docs from ProxyRequest.GetTimeout()
func (request *WorkflowRegisterRequest) GetTimeout() time.Duration {
	return request.ProxyRequest.GetTimeout()
}

// SetTimeout inherits docs from ProxyRequest.SetTimeout()
func (request *WorkflowRegisterRequest) SetTimeout(value time.Duration) {
	request.ProxyRequest.SetTimeout(value)
}
