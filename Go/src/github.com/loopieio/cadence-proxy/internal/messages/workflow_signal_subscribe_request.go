package messages

import (
	"time"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowSignalSubscribeRequest is WorkflowRequest of MessageType
	// WorkflowSignalSubscribeRequest.
	//
	// A WorkflowSignalSubscribeRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// A WorkflowSignalSubscribeRequest will pass all of the given information
	// necessary to subscribe a workflow to a named signal
	WorkflowSignalSubscribeRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowSignalSubscribeRequest is the default constructor for a WorkflowSignalSubscribeRequest
//
// returns *WorkflowSignalSubscribeRequest -> a reference to a newly initialized
// WorkflowSignalSubscribeRequest in memory
func NewWorkflowSignalSubscribeRequest() *WorkflowSignalSubscribeRequest {
	request := new(WorkflowSignalSubscribeRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(messagetypes.WorkflowSignalSubscribeRequest)
	request.SetReplyType(messagetypes.WorkflowSignalSubscribeReply)

	return request
}

// GetSignalName gets a WorkflowSignalSubscribeRequest's SignalName value
// from its properties map. Identifies the signal being subscribed.
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowSignalSubscribeRequest's SignalName
func (request *WorkflowSignalSubscribeRequest) GetSignalName() *string {
	return request.GetStringProperty("SignalName")
}

// SetSignalName sets a WorkflowSignalSubscribeRequest's SignalName value
// in its properties map. Identifies the signal being subscribed.
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowSignalSubscribeRequest) SetSignalName(value *string) {
	request.SetStringProperty("SignalName", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowSignalSubscribeRequest) Clone() IProxyMessage {
	workflowSignalSubscribeRequest := NewWorkflowSignalSubscribeRequest()
	var messageClone IProxyMessage = workflowSignalSubscribeRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowSignalSubscribeRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowSignalSubscribeRequest); ok {
		v.SetSignalName(request.GetSignalName())
	}
}

// SetProxyMessage inherits docs from WorkflowRequest.SetProxyMessage()
func (request *WorkflowSignalSubscribeRequest) SetProxyMessage(value *ProxyMessage) {
	request.WorkflowRequest.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from WorkflowRequest.GetProxyMessage()
func (request *WorkflowSignalSubscribeRequest) GetProxyMessage() *ProxyMessage {
	return request.WorkflowRequest.GetProxyMessage()
}

// GetRequestID inherits docs from WorkflowRequest.GetRequestID()
func (request *WorkflowSignalSubscribeRequest) GetRequestID() int64 {
	return request.WorkflowRequest.GetRequestID()
}

// SetRequestID inherits docs from WorkflowRequest.SetRequestID()
func (request *WorkflowSignalSubscribeRequest) SetRequestID(value int64) {
	request.WorkflowRequest.SetRequestID(value)
}

// GetType inherits docs from WorkflowRequest.GetType()
func (request *WorkflowSignalSubscribeRequest) GetType() messagetypes.MessageType {
	return request.WorkflowRequest.GetType()
}

// SetType inherits docs from WorkflowRequest.SetType()
func (request *WorkflowSignalSubscribeRequest) SetType(value messagetypes.MessageType) {
	request.WorkflowRequest.SetType(value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType inherits docs from WorkflowRequest.GetReplyType()
func (request *WorkflowSignalSubscribeRequest) GetReplyType() messagetypes.MessageType {
	return request.WorkflowRequest.GetReplyType()
}

// SetReplyType inherits docs from WorkflowRequest.SetReplyType()
func (request *WorkflowSignalSubscribeRequest) SetReplyType(value messagetypes.MessageType) {
	request.WorkflowRequest.SetReplyType(value)
}

// GetTimeout inherits docs from WorkflowRequest.GetTimeout()
func (request *WorkflowSignalSubscribeRequest) GetTimeout() time.Duration {
	return request.WorkflowRequest.GetTimeout()
}

// SetTimeout inherits docs from WorkflowRequest.SetTimeout()
func (request *WorkflowSignalSubscribeRequest) SetTimeout(value time.Duration) {
	request.WorkflowRequest.SetTimeout(value)
}

// -------------------------------------------------------------------------
// IWorkflowRequest interface methods for implementing the IWorkflowRequest interface

// GetContextID inherits docs from WorkflowRequest.GetContextID()
func (request *WorkflowSignalSubscribeRequest) GetContextID() int64 {
	return request.WorkflowRequest.GetContextID()
}

// SetContextID inherits docs from WorkflowRequest.GetContextID()
func (request *WorkflowSignalSubscribeRequest) SetContextID(value int64) {
	request.WorkflowRequest.SetContextID(value)
}
