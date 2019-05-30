package messages

import (
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
