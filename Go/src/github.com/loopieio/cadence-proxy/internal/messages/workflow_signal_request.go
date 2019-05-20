package messages

import (
	"time"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowSignalRequest is ProxyRequest of MessageType
	// WorkflowSignalRequest.
	//
	// A WorkflowSignalRequest contains a reference to a
	// ProxyRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ProxyRequest
	//
	// A WorkflowSignalRequest will pass all of the given data and options
	// necessary to signal a cadence workflow via the cadence client
	WorkflowSignalRequest struct {
		*ProxyRequest
	}
)

// NewWorkflowSignalRequest is the default constructor for a WorkflowSignalRequest
//
// returns *WorkflowSignalRequest -> a reference to a newly initialized
// WorkflowSignalRequest in memory
func NewWorkflowSignalRequest() *WorkflowSignalRequest {
	request := new(WorkflowSignalRequest)
	request.ProxyRequest = NewProxyRequest()
	request.Type = messagetypes.WorkflowSignalRequest
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

// Clone inherits docs from ProxyRequest.Clone()
func (request *WorkflowSignalRequest) Clone() IProxyMessage {
	workflowSignalRequest := NewWorkflowSignalRequest()
	var messageClone IProxyMessage = workflowSignalRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyRequest.CopyTo()
func (request *WorkflowSignalRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
	if v, ok := target.(*WorkflowSignalRequest); ok {
		v.SetWorkflowID(request.GetWorkflowID())
		v.SetRunID(request.GetRunID())
		v.SetSignalName(request.GetSignalName())
		v.SetSignalArgs(request.GetSignalArgs())
	}
}

// SetProxyMessage inherits docs from ProxyRequest.SetProxyMessage()
func (request *WorkflowSignalRequest) SetProxyMessage(value *ProxyMessage) {
	request.ProxyRequest.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ProxyRequest.GetProxyMessage()
func (request *WorkflowSignalRequest) GetProxyMessage() *ProxyMessage {
	return request.ProxyRequest.GetProxyMessage()
}

// GetRequestID inherits docs from ProxyRequest.GetRequestID()
func (request *WorkflowSignalRequest) GetRequestID() int64 {
	return request.ProxyRequest.GetRequestID()
}

// SetRequestID inherits docs from ProxyRequest.SetRequestID()
func (request *WorkflowSignalRequest) SetRequestID(value int64) {
	request.ProxyRequest.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType inherits docs from ProxyRequest.GetReplyType()
func (request *WorkflowSignalRequest) GetReplyType() messagetypes.MessageType {
	return request.ProxyRequest.GetReplyType()
}

// SetReplyType inherits docs from ProxyRequest.SetReplyType()
func (request *WorkflowSignalRequest) SetReplyType(value messagetypes.MessageType) {
	request.ProxyRequest.SetReplyType(value)
}

// GetTimeout inherits docs from ProxyRequest.GetTimeout()
func (request *WorkflowSignalRequest) GetTimeout() time.Duration {
	return request.ProxyRequest.GetTimeout()
}

// SetTimeout inherits docs from ProxyRequest.SetTimeout()
func (request *WorkflowSignalRequest) SetTimeout(value time.Duration) {
	request.ProxyRequest.SetTimeout(value)
}
