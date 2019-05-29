package messages

import (
	"time"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowSignalRequest is WorkflowRequest of MessageType
	// WorkflowSignalRequest.
	//
	// A WorkflowSignalRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// A WorkflowSignalRequest will pass all of the given data and options
	// necessary to signal a cadence workflow via the cadence client
	WorkflowSignalRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowSignalRequest is the default constructor for a WorkflowSignalRequest
//
// returns *WorkflowSignalRequest -> a reference to a newly initialized
// WorkflowSignalRequest in memory
func NewWorkflowSignalRequest() *WorkflowSignalRequest {
	request := new(WorkflowSignalRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(messagetypes.WorkflowSignalRequest)
	request.SetReplyType(messagetypes.WorkflowSignalReply)

	return request
}

// GetWorkflowID gets a WorkflowSignalRequest's WorkflowID value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowSignalRequest's WorkflowID
func (request *WorkflowSignalRequest) GetWorkflowID() *string {
	return request.GetStringProperty("WorkflowId")
}

// SetWorkflowID sets an WorkflowSignalRequest's WorkflowID value
// in its properties map
//
// param value *string -> pointer to a string in memory holding the value
// of a WorkflowSignalRequest's WorkflowID
func (request *WorkflowSignalRequest) SetWorkflowID(value *string) {
	request.SetStringProperty("WorkflowId", value)
}

// GetRunID gets a WorkflowSignalRequest's RunID value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowSignalRequest's RunID
func (request *WorkflowSignalRequest) GetRunID() *string {
	return request.GetStringProperty("RunId")
}

// SetRunID sets a WorkflowSignalRequest's RunID value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowSignalRequest) SetRunID(value *string) {
	request.SetStringProperty("RunId", value)
}

// GetSignalName gets a WorkflowSignalRequest's SignalName value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowSignalRequest's SignalName
func (request *WorkflowSignalRequest) GetSignalName() *string {
	return request.GetStringProperty("SignalName")
}

// SetSignalName sets a WorkflowSignalRequest's SignalName value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowSignalRequest) SetSignalName(value *string) {
	request.SetStringProperty("SignalName", value)
}

// GetSignalArgs gets a WorkflowSignalRequest's SignalArgs field
// from its properties map.  SignalArgs is a []byte holding the arguments
// for signaling a specific workflow
//
// returns []byte -> a []byte of representing workflow parameters
// or arguments for invoking
func (request *WorkflowSignalRequest) GetSignalArgs() []byte {
	return request.GetBytesProperty("SignalArgs")
}

// SetSignalArgs sets an WorkflowSignalRequest's SignalArgs field
// from its properties map.  SignalArgs is a []byte holding the arguments
// for signaling a specific workflow
//
// param value []byte -> []byte of representing workflow parameters
// or arguments for invoking
func (request *WorkflowSignalRequest) SetSignalArgs(value []byte) {
	request.SetBytesProperty("SignalArgs", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowSignalRequest) Clone() IProxyMessage {
	workflowSignalRequest := NewWorkflowSignalRequest()
	var messageClone IProxyMessage = workflowSignalRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowSignalRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowSignalRequest); ok {
		v.SetWorkflowID(request.GetWorkflowID())
		v.SetRunID(request.GetRunID())
		v.SetSignalName(request.GetSignalName())
		v.SetSignalArgs(request.GetSignalArgs())
	}
}

// SetProxyMessage inherits docs from WorkflowRequest.SetProxyMessage()
func (request *WorkflowSignalRequest) SetProxyMessage(value *ProxyMessage) {
	request.WorkflowRequest.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from WorkflowRequest.GetProxyMessage()
func (request *WorkflowSignalRequest) GetProxyMessage() *ProxyMessage {
	return request.WorkflowRequest.GetProxyMessage()
}

// GetRequestID inherits docs from WorkflowRequest.GetRequestID()
func (request *WorkflowSignalRequest) GetRequestID() int64 {
	return request.WorkflowRequest.GetRequestID()
}

// SetRequestID inherits docs from WorkflowRequest.SetRequestID()
func (request *WorkflowSignalRequest) SetRequestID(value int64) {
	request.WorkflowRequest.SetRequestID(value)
}

// GetType inherits docs from WorkflowRequest.GetType()
func (request *WorkflowSignalRequest) GetType() messagetypes.MessageType {
	return request.WorkflowRequest.GetType()
}

// SetType inherits docs from WorkflowRequest.SetType()
func (request *WorkflowSignalRequest) SetType(value messagetypes.MessageType) {
	request.WorkflowRequest.SetType(value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType inherits docs from WorkflowRequest.GetReplyType()
func (request *WorkflowSignalRequest) GetReplyType() messagetypes.MessageType {
	return request.WorkflowRequest.GetReplyType()
}

// SetReplyType inherits docs from WorkflowRequest.SetReplyType()
func (request *WorkflowSignalRequest) SetReplyType(value messagetypes.MessageType) {
	request.WorkflowRequest.SetReplyType(value)
}

// GetTimeout inherits docs from WorkflowRequest.GetTimeout()
func (request *WorkflowSignalRequest) GetTimeout() time.Duration {
	return request.WorkflowRequest.GetTimeout()
}

// SetTimeout inherits docs from WorkflowRequest.SetTimeout()
func (request *WorkflowSignalRequest) SetTimeout(value time.Duration) {
	request.WorkflowRequest.SetTimeout(value)
}

// -------------------------------------------------------------------------
// IWorkflowRequest interface methods for implementing the IWorkflowRequest interface

// GetContextID inherits docs from WorkflowRequest.GetContextID()
func (request *WorkflowSignalRequest) GetContextID() int64 {
	return request.WorkflowRequest.GetContextID()
}

// SetContextID inherits docs from WorkflowRequest.GetContextID()
func (request *WorkflowSignalRequest) SetContextID(value int64) {
	request.WorkflowRequest.SetContextID(value)
}
