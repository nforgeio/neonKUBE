package messages

import (
	"time"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"

	"go.uber.org/cadence/client"
)

type (

	// WorkflowSignalWithStartRequest is ProxyRequest of MessageType
	// WorkflowSignalWithStartRequest.
	//
	// A WorkflowSignalWithStartRequest contains a reference to a
	// ProxyRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ProxyRequest
	//
	// A WorkflowSignalWithStartRequest will pass all of the given data and options
	// necessary to signal a cadence workflow with start via the cadence client
	WorkflowSignalWithStartRequest struct {
		*ProxyRequest
	}
)

// NewWorkflowSignalWithStartRequest is the default constructor for a WorkflowSignalWithStartRequest
//
// returns *WorkflowSignalWithStartRequest -> a reference to a newly initialized
// WorkflowSignalWithStartRequest in memory
func NewWorkflowSignalWithStartRequest() *WorkflowSignalWithStartRequest {
	request := new(WorkflowSignalWithStartRequest)
	request.ProxyRequest = NewProxyRequest()
	request.Type = messagetypes.WorkflowSignalWithStartRequest
	request.SetReplyType(messagetypes.WorkflowSignalWithStartReply)

	return request
}

// GetWorkflowID gets a WorkflowSignalWithStartRequest's WorkflowID value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowSignalWithStartRequest's WorkflowID
func (request *WorkflowSignalWithStartRequest) GetWorkflowID() *string {
	return request.GetStringProperty("WorkflowId")
}

// SetWorkflowID sets an WorkflowSignalWithStartRequest's WorkflowID value
// in its properties map
//
// param value *string -> pointer to a string in memory holding the value
// of a WorkflowSignalWithStartRequest's WorkflowID
func (request *WorkflowSignalWithStartRequest) SetWorkflowID(value *string) {
	request.SetStringProperty("WorkflowId", value)
}

// GetSignalName gets a WorkflowSignalWithStartRequest's SignalName value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowSignalWithStartRequest's SignalName
func (request *WorkflowSignalWithStartRequest) GetSignalName() *string {
	return request.GetStringProperty("SignalName")
}

// SetSignalName sets a WorkflowSignalWithStartRequest's SignalName value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowSignalWithStartRequest) SetSignalName(value *string) {
	request.SetStringProperty("SignalName", value)
}

// GetSignalArgs gets a WorkflowSignalWithStartRequest's SignalArgs field
// from its properties map.  SignalArgs is a []byte that hold the arguments
// for executing a specific workflow
//
// returns []byte -> []byte representing workflow parameters or arguments
// for executing
func (request *WorkflowSignalWithStartRequest) GetSignalArgs() []byte {
	return request.GetBytesProperty("SignalArgs")
}

// SetSignalArgs sets an WorkflowSignalWithStartRequest's SignalArgs field
// from its properties map.  SignalArgs is a []byte that hold the arguments
// for signaling a specific workflow with start
//
// param value []byte -> []byte representing workflow parameters or arguments
// for executing
func (request *WorkflowSignalWithStartRequest) SetSignalArgs(value []byte) {
	request.SetBytesProperty("SignalArgs", value)
}

// GetOptions gets a WorkflowSignalWithStartRequest's start options
// used to signal a cadence workflow with start options via the client
//
// returns client.StartWorkflowOptions -> a cadence client struct that contains the
// options for executing a workflow
func (request *WorkflowSignalWithStartRequest) GetOptions() *client.StartWorkflowOptions {
	opts := new(client.StartWorkflowOptions)
	err := request.GetJSONProperty("Options", opts)
	if err != nil {
		return nil
	}

	return opts
}

// SetOptions sets a WorkflowSignalWithStartRequest's start options
// used to signal a cadence workflow with start options via the client
//
// param value client.StartWorkflowOptions -> a cadence client struct that contains the
// options for executing a workflow to be set in the WorkflowSignalWithStartRequest's
// properties map
func (request *WorkflowSignalWithStartRequest) SetOptions(value *client.StartWorkflowOptions) {
	request.SetJSONProperty("Options", value)
}

// GetWorkflowArgs gets a WorkflowSignalWithStartRequest's WorkflowArgs field
// from its properties map.  WorkflowArgs is a []byte that hold the arguments
// for executing a specific workflow
//
// returns []byte -> []byte representing workflow parameters or arguments
// for executing
func (request *WorkflowSignalWithStartRequest) GetWorkflowArgs() []byte {
	return request.GetBytesProperty("WorkflowArgs")
}

// SetWorkflowArgs sets an WorkflowSignalWithStartRequest's WorkflowArgs field
// from its properties map.  WorkflowArgs is a []byte that hold the arguments
// for signaling a specific workflow with start
//
// param value []byte -> []byte representing workflow parameters or arguments
// for executing
func (request *WorkflowSignalWithStartRequest) SetWorkflowArgs(value []byte) {
	request.SetBytesProperty("WorkflowArgs", value)
}

// GetWorkflow gets a WorkflowSignalWithStartRequest's Workflow value
// from its properties map. Identifies the workflow implementation to be started.
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowSignalWithStartRequest's Workflow
func (request *WorkflowSignalWithStartRequest) GetWorkflow() *string {
	return request.GetStringProperty("Workflow")
}

// SetWorkflow sets a WorkflowSignalWithStartRequest's Workflow value
// in its properties map. Identifies the workflow implementation to be started.
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowSignalWithStartRequest) SetWorkflow(value *string) {
	request.SetStringProperty("Workflow", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyRequest.Clone()
func (request *WorkflowSignalWithStartRequest) Clone() IProxyMessage {
	workflowSignalWithStartRequest := NewWorkflowSignalWithStartRequest()
	var messageClone IProxyMessage = workflowSignalWithStartRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyRequest.CopyTo()
func (request *WorkflowSignalWithStartRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
	if v, ok := target.(*WorkflowSignalWithStartRequest); ok {
		v.SetWorkflowID(request.GetWorkflowID())
		v.SetSignalName(request.GetSignalName())
		v.SetSignalArgs(request.GetSignalArgs())
		v.SetOptions(request.GetOptions())
		v.SetWorkflowArgs(request.GetWorkflowArgs())
		v.SetWorkflow(request.GetWorkflow())
	}
}

// SetProxyMessage inherits docs from ProxyRequest.SetProxyMessage()
func (request *WorkflowSignalWithStartRequest) SetProxyMessage(value *ProxyMessage) {
	request.ProxyRequest.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ProxyRequest.GetProxyMessage()
func (request *WorkflowSignalWithStartRequest) GetProxyMessage() *ProxyMessage {
	return request.ProxyRequest.GetProxyMessage()
}

// GetRequestID inherits docs from ProxyRequest.GetRequestID()
func (request *WorkflowSignalWithStartRequest) GetRequestID() int64 {
	return request.ProxyRequest.GetRequestID()
}

// SetRequestID inherits docs from ProxyRequest.SetRequestID()
func (request *WorkflowSignalWithStartRequest) SetRequestID(value int64) {
	request.ProxyRequest.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType inherits docs from ProxyRequest.GetReplyType()
func (request *WorkflowSignalWithStartRequest) GetReplyType() messagetypes.MessageType {
	return request.ProxyRequest.GetReplyType()
}

// SetReplyType inherits docs from ProxyRequest.SetReplyType()
func (request *WorkflowSignalWithStartRequest) SetReplyType(value messagetypes.MessageType) {
	request.ProxyRequest.SetReplyType(value)
}

// GetTimeout inherits docs from ProxyRequest.GetTimeout()
func (request *WorkflowSignalWithStartRequest) GetTimeout() time.Duration {
	return request.ProxyRequest.GetTimeout()
}

// SetTimeout inherits docs from ProxyRequest.SetTimeout()
func (request *WorkflowSignalWithStartRequest) SetTimeout(value time.Duration) {
	request.ProxyRequest.SetTimeout(value)
}
