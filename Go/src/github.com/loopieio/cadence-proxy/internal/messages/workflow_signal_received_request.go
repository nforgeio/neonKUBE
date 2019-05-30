package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowSignalReceivedRequest is WorkflowRequest of MessageType
	// WorkflowSignalReceivedRequest.
	//
	// A WorkflowSignalReceivedRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// A WorkflowSignalReceivedRequest sends a received signal to a running workflow.
	WorkflowSignalReceivedRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowSignalReceivedRequest is the default constructor for a WorkflowSignalReceivedRequest
//
// returns *WorkflowSignalReceivedRequest -> a reference to a newly initialized
// WorkflowSignalReceivedRequest in memory
func NewWorkflowSignalReceivedRequest() *WorkflowSignalReceivedRequest {
	request := new(WorkflowSignalReceivedRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(messagetypes.WorkflowSignalReceivedRequest)
	request.SetReplyType(messagetypes.WorkflowSignalReceivedReply)

	return request
}

// GetSignalName gets a WorkflowSignalReceivedRequest's SignalName value
// from its properties map. Identifies the signal.
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowSignalReceivedRequest's SignalName
func (request *WorkflowSignalReceivedRequest) GetSignalName() *string {
	return request.GetStringProperty("SignalName")
}

// SetSignalName sets a WorkflowSignalReceivedRequest's SignalName value
// in its properties map. Identifies the signal.
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowSignalReceivedRequest) SetSignalName(value *string) {
	request.SetStringProperty("SignalName", value)
}

// GetSignalArgs gets a WorkflowSignalReceivedRequest's SignalArgs field
// from its properties map.  SignalArgs is a []byte holding the arguments
// for signaling a specific workflow
//
// returns []byte -> a []byte of representing workflow parameters
// or arguments for invoking
func (request *WorkflowSignalReceivedRequest) GetSignalArgs() []byte {
	return request.GetBytesProperty("SignalArgs")
}

// SetSignalArgs sets an WorkflowSignalReceivedRequest's SignalArgs field
// from its properties map.  SignalArgs is a []byte holding the arguments
// for signaling a specific workflow
//
// param value []byte -> []byte of representing workflow parameters
// or arguments for invoking
func (request *WorkflowSignalReceivedRequest) SetSignalArgs(value []byte) {
	request.SetBytesProperty("SignalArgs", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowSignalReceivedRequest) Clone() IProxyMessage {
	workflowSignalReceivedRequest := NewWorkflowSignalReceivedRequest()
	var messageClone IProxyMessage = workflowSignalReceivedRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowSignalReceivedRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowSignalReceivedRequest); ok {
		v.SetSignalName(request.GetSignalName())
		v.SetSignalArgs(request.GetSignalArgs())
	}
}
