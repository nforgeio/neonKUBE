package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowRegisterRequest is WorkflowRequest of MessageType
	// WorkflowRegisterRequest.
	//
	// A WorkflowRegisterRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// A WorkflowRegisterRequest will pass all of the given information
	// necessary to register a workflow function with the cadence server
	// via the cadence client
	WorkflowRegisterRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowRegisterRequest is the default constructor for a WorkflowRegisterRequest
//
// returns *WorkflowRegisterRequest -> a reference to a newly initialized
// WorkflowRegisterRequest in memory
func NewWorkflowRegisterRequest() *WorkflowRegisterRequest {
	request := new(WorkflowRegisterRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(messagetypes.WorkflowRegisterRequest)
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

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowRegisterRequest) Clone() IProxyMessage {
	workflowRegisterRequest := NewWorkflowRegisterRequest()
	var messageClone IProxyMessage = workflowRegisterRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowRegisterRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowRegisterRequest); ok {
		v.SetName(request.GetName())
	}
}
