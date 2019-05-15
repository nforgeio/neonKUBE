package types

import (
	"github.com/loopieio/cadence-proxy/internal/messages"
	"go.uber.org/cadence/client"
)

type (

	// WorkflowExecuteRequest is ProxyRequest of MessageType
	// WorkflowExecuteRequest.
	//
	// A WorkflowExecuteRequest contains a reference to a
	// ProxyRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ProxyRequest
	//
	// A WorkflowExecuteRequest will pass all of the given data and options
	// necessary to execute a cadence workflow via the cadence client
	WorkflowExecuteRequest struct {
		*ProxyRequest
	}
)

// NewWorkflowExecuteRequest is the default constructor for a WorkflowExecuteRequest
//
// returns *WorkflowExecuteRequest -> a reference to a newly initialized
// WorkflowExecuteRequest in memory
func NewWorkflowExecuteRequest() *WorkflowExecuteRequest {
	request := new(WorkflowExecuteRequest)
	request.ProxyRequest = NewProxyRequest()
	request.Type = messages.WorkflowExecuteRequest
	request.SetReplyType(messages.WorkflowExecuteReply)

	return request
}

// GetDomain gets a WorkflowExecuteRequest's Domain value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowExecuteRequest's Domain
func (request *WorkflowExecuteRequest) GetDomain() *string {
	return request.GetStringProperty("Domain")
}

// SetDomain sets an WorkflowExecuteRequest's Domain value
// in its properties map
//
// param value *string -> pointer to a string in memory holding the value
// of a WorkflowExecuteRequest's Domain
func (request *WorkflowExecuteRequest) SetDomain(value *string) {
	request.SetStringProperty("Domain", value)
}

// GetName gets a WorkflowExecuteRequest's Name value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowExecuteRequest's Name
func (request *WorkflowExecuteRequest) GetName() *string {
	return request.GetStringProperty("Name")
}

// SetName sets a WorkflowExecuteRequest's Name value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowExecuteRequest) SetName(value *string) {
	request.SetStringProperty("Name", value)
}

// GetArgs gets a WorkflowExecuteRequest's Args field
// from its properties map.  Args is a map of key/value
// pairs that hold the arguments for executing a specific workflow
//
// returns map[string]interface{]} -> a map of strings to values
// representing workflow parameters or arguments for executing
func (request *WorkflowExecuteRequest) GetArgs() map[string]interface{} {
	args := request.GetJSONProperty("Args", make(map[string]interface{}))
	if v, ok := args.(map[string]interface{}); ok {
		return v
	}

	return nil
}

// SetArgs sets an WorkflowExecuteRequest's Args field
// from its properties map.  Args is a map of key/value
// pairs that hold the arguments for executing a specific workflow
//
// param value map[string]interface{]} -> a map of strings to values
// representing workflow parameters or arguments for executing
func (request *WorkflowExecuteRequest) SetArgs(value map[string]interface{}) {
	request.SetJSONProperty("Args", value)
}

// GetOptions gets a WorkflowExecutionRequest's start options
// used to execute a cadence workflow via the cadence workflow client
//
// returns client.StartWorkflowOptions -> a cadence client struct that contains the
// options for executing a workflow
func (request *WorkflowExecuteRequest) GetOptions() *client.StartWorkflowOptions {
	opts := request.GetJSONProperty("Options", new(client.StartWorkflowOptions))
	if v, ok := opts.(*client.StartWorkflowOptions); ok {
		return v
	}

	return nil
}

// SetOptions sets a WorkflowExecutionRequest's start options
// used to execute a cadence workflow via the cadence workflow client
//
// param value client.StartWorkflowOptions -> a cadence client struct that contains the
// options for executing a workflow to be set in the WorkflowExecutionRequest's
// properties map
func (request *WorkflowExecuteRequest) SetOptions(value *client.StartWorkflowOptions) {
	request.SetJSONProperty("Options", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (request *WorkflowExecuteRequest) Clone() IProxyMessage {
	WorkflowExecuteRequest := NewWorkflowExecuteRequest()
	var messageClone IProxyMessage = WorkflowExecuteRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (request *WorkflowExecuteRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
	if v, ok := target.(*WorkflowExecuteRequest); ok {
		v.SetDomain(request.GetDomain())
		v.SetName(request.GetName())
		v.SetArgs(request.GetArgs())
		v.SetOptions(request.GetOptions())
	}
}

// SetProxyMessage inherits docs from ProxyMessage.SetProxyMessage()
func (request *WorkflowExecuteRequest) SetProxyMessage(value *ProxyMessage) {
	request.ProxyMessage.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ProxyMessage.GetProxyMessage()
func (request *WorkflowExecuteRequest) GetProxyMessage() *ProxyMessage {
	return request.ProxyMessage.GetProxyMessage()
}

// GetRequestID inherits docs from ProxyMessage.GetRequestID()
func (request *WorkflowExecuteRequest) GetRequestID() int64 {
	return request.ProxyMessage.GetRequestID()
}

// SetRequestID inherits docs from ProxyMessage.SetRequestID()
func (request *WorkflowExecuteRequest) SetRequestID(value int64) {
	request.ProxyMessage.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType inherits docs from ProxyRequest.GetReplyType()
func (request *WorkflowExecuteRequest) GetReplyType() messages.MessageType {
	return request.ProxyRequest.GetReplyType()
}

// SetReplyType inherits docs from ProxyRequest.SetReplyType()
func (request *WorkflowExecuteRequest) SetReplyType(value messages.MessageType) {
	request.ProxyRequest.SetReplyType(value)
}
