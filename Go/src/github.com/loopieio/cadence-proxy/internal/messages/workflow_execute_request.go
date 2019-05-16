package messages

import (
	"time"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"

	"go.uber.org/cadence/client"
)

type (

	// WorkflowExecuteRequest is WorkflowRequest of MessageType
	// WorkflowExecuteRequest.
	//
	// A WorkflowExecuteRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// A WorkflowExecuteRequest will pass all of the given data and options
	// necessary to execute a cadence workflow via the cadence client
	WorkflowExecuteRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowExecuteRequest is the default constructor for a WorkflowExecuteRequest
//
// returns *WorkflowExecuteRequest -> a reference to a newly initialized
// WorkflowExecuteRequest in memory
func NewWorkflowExecuteRequest() *WorkflowExecuteRequest {
	request := new(WorkflowExecuteRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.Type = messagetypes.WorkflowExecuteRequest
	request.SetReplyType(messagetypes.WorkflowExecuteReply)

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
// returns *map[string]interface{]} -> pointer to a map of strings to values
// representing workflow parameters or arguments for executing
func (request *WorkflowExecuteRequest) GetArgs() *map[string]interface{} {
	args := new(map[string]interface{})
	err := request.GetJSONProperty("Args", args)
	if err != nil {
		return nil
	}

	return args
}

// SetArgs sets an WorkflowExecuteRequest's Args field
// from its properties map.  Args is a map of key/value
// pairs that hold the arguments for executing a specific workflow
//
// param value map[string]interface{]} -> pointer to a map of strings to values
// representing workflow parameters or arguments for executing
func (request *WorkflowExecuteRequest) SetArgs(value *map[string]interface{}) {
	request.SetJSONProperty("Args", value)
}

// GetOptions gets a WorkflowExecutionRequest's start options
// used to execute a cadence workflow via the cadence workflow client
//
// returns client.StartWorkflowOptions -> a cadence client struct that contains the
// options for executing a workflow
func (request *WorkflowExecuteRequest) GetOptions() *client.StartWorkflowOptions {
	opts := new(client.StartWorkflowOptions)
	err := request.GetJSONProperty("Options", opts)
	if err != nil {
		return nil
	}

	return opts
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

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowExecuteRequest) Clone() IProxyMessage {
	workflowExecuteRequest := NewWorkflowExecuteRequest()
	var messageClone IProxyMessage = workflowExecuteRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowExecuteRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowExecuteRequest); ok {
		v.SetDomain(request.GetDomain())
		v.SetName(request.GetName())
		v.SetArgs(request.GetArgs())
		v.SetOptions(request.GetOptions())
	}
}

// SetProxyMessage inherits docs from WorkflowRequest.SetProxyMessage()
func (request *WorkflowExecuteRequest) SetProxyMessage(value *ProxyMessage) {
	request.WorkflowRequest.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from WorkflowRequest.GetProxyMessage()
func (request *WorkflowExecuteRequest) GetProxyMessage() *ProxyMessage {
	return request.WorkflowRequest.GetProxyMessage()
}

// GetRequestID inherits docs from WorkflowRequest.GetRequestID()
func (request *WorkflowExecuteRequest) GetRequestID() int64 {
	return request.WorkflowRequest.GetRequestID()
}

// SetRequestID inherits docs from WorkflowRequest.SetRequestID()
func (request *WorkflowExecuteRequest) SetRequestID(value int64) {
	request.WorkflowRequest.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType inherits docs from WorkflowRequest.GetReplyType()
func (request *WorkflowExecuteRequest) GetReplyType() messagetypes.MessageType {
	return request.WorkflowRequest.GetReplyType()
}

// SetReplyType inherits docs from WorkflowRequest.SetReplyType()
func (request *WorkflowExecuteRequest) SetReplyType(value messagetypes.MessageType) {
	request.WorkflowRequest.SetReplyType(value)
}

// GetTimeout inherits docs from WorkflowRequest.GetTimeout()
func (request *WorkflowExecuteRequest) GetTimeout() time.Duration {
	return request.WorkflowRequest.GetTimeout()
}

// SetTimeout inherits docs from WorkflowRequest.SetTimeout()
func (request *WorkflowExecuteRequest) SetTimeout(value time.Duration) {
	request.WorkflowRequest.SetTimeout(value)
}

// -------------------------------------------------------------------------
// IWorkflowRequest interface methods for implementing the IWorkflowRequest interface

// GetContextID inherits docs from WorkflowRequest.GetContextID()
func (request *WorkflowExecuteRequest) GetContextID() int64 {
	return request.WorkflowRequest.GetContextID()
}

// SetContextID inherits docs from WorkflowRequest.SetContextID()
func (request *WorkflowExecuteRequest) SetContextID(value int64) {
	request.WorkflowRequest.SetContextID(value)
}
