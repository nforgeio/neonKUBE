package types

import (
	"github.com/loopieio/cadence-proxy/internal/messages"
)

type (

	// WorkflowInvokeRequest is ProxyRequest of MessageType
	// WorkflowInvokeRequest.
	//
	// A WorkflowInvokeRequest contains a RequestId and a reference to a
	// ProxyRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ProxyRequest
	//
	// A WorkflowInvokeRequest will pass all of the given information
	// necessary to invoke a cadence workflow via the cadence client
	WorkflowInvokeRequest struct {
		*ProxyRequest
	}
)

// NewWorkflowInvokeRequest is the default constructor for a WorkflowInvokeRequest
//
// returns *WorkflowInvokeRequest -> a reference to a newly initialized
// WorkflowInvokeRequest in memory
func NewWorkflowInvokeRequest() *WorkflowInvokeRequest {
	request := new(WorkflowInvokeRequest)
	request.ProxyRequest = NewProxyRequest()
	request.Type = messages.WorkflowInvokeRequest
	request.SetReplyType(messages.WorkflowInvokeReply)

	return request
}

// GetName gets a WorkflowInvokeRequest's Name value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowInvokeRequest's Name
func (request *WorkflowInvokeRequest) GetName() *string {
	return request.GetStringProperty("Name")
}

// SetName sets a WorkflowInvokeRequest's Name value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowInvokeRequest) SetName(value *string) {
	request.SetStringProperty("Name", value)
}

// GetArgs gets a WorkflowInvokeRequest's Args field
// from its properties map.  Args is a map of key/value
// pairs that hold the arguments for invoking a specific workflow
//
// returns map[string]interface{]} -> a map of strings to values
// representing workflow parameters or arguments for invoking
func (request *WorkflowInvokeRequest) GetArgs() *map[string]interface{} {
	args := request.GetJSONProperty("Args", make(map[string]interface{}))
	if v, ok := args.(*map[string]interface{}); ok {
		return v
	}

	return nil
}

// SetArgs sets an WorkflowInvokeRequest's Args field
// from its properties map.  Args is a map of key/value
// pairs that hold the arguments for invoking a specific workflow
//
// param value map[string]interface{]} -> a map of strings to values
// representing workflow parameters or arguments for invoking
func (request *WorkflowInvokeRequest) SetArgs(value *map[string]interface{}) {
	request.SetJSONProperty("Args", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (request *WorkflowInvokeRequest) Clone() IProxyMessage {
	WorkflowInvokeRequest := NewWorkflowInvokeRequest()
	var messageClone IProxyMessage = WorkflowInvokeRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (request *WorkflowInvokeRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
	if v, ok := target.(*WorkflowInvokeRequest); ok {
		v.SetName(request.GetName())
		v.SetArgs(request.GetArgs())
	}
}

// SetProxyMessage inherits docs from ProxyMessage.SetProxyMessage()
func (request *WorkflowInvokeRequest) SetProxyMessage(value *ProxyMessage) {
	request.ProxyMessage.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ProxyMessage.GetProxyMessage()
func (request *WorkflowInvokeRequest) GetProxyMessage() *ProxyMessage {
	return request.ProxyMessage.GetProxyMessage()
}

// GetRequestID inherits docs from ProxyMessage.GetRequestID()
func (request *WorkflowInvokeRequest) GetRequestID() int64 {
	return request.ProxyMessage.GetRequestID()
}

// SetRequestID inherits docs from ProxyMessage.SetRequestID()
func (request *WorkflowInvokeRequest) SetRequestID(value int64) {
	request.ProxyMessage.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType inherits docs from ProxyRequest.GetReplyType()
func (request *WorkflowInvokeRequest) GetReplyType() messages.MessageType {
	return request.ProxyRequest.GetReplyType()
}

// SetReplyType inherits docs from ProxyRequest.SetReplyType()
func (request *WorkflowInvokeRequest) SetReplyType(value messages.MessageType) {
	request.ProxyRequest.SetReplyType(value)
}
