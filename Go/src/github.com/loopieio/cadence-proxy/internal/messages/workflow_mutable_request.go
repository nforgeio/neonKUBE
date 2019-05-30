package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowMutableRequest is WorkflowRequest of MessageType
	// WorkflowMutableRequest.
	//
	// A WorkflowMutableRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// A WorkflowMutableRequest will pass all of the given data
	// necessary to invoke a cadence workflow instance via the cadence client
	WorkflowMutableRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowMutableRequest is the default constructor for a WorkflowMutableRequest
//
// returns *WorkflowMutableRequest -> a reference to a newly initialized
// WorkflowMutableRequest in memory
func NewWorkflowMutableRequest() *WorkflowMutableRequest {
	request := new(WorkflowMutableRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(messagetypes.WorkflowMutableRequest)
	request.SetReplyType(messagetypes.WorkflowMutableReply)

	return request
}

// GetMutableID gets a WorkflowMutableRequest's MutableID value
// from its properties map. Identifies the mutable value to be returned.
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowMutableRequest's MutableID
func (request *WorkflowMutableRequest) GetMutableID() *string {
	return request.GetStringProperty("MutableId")
}

// SetMutableID sets an WorkflowMutableRequest's MutableID value
// in its properties map. Identifies the mutable value to be returned.
//
// param value *string -> pointer to a string in memory holding the value
// of a WorkflowMutableRequest's MutableID
func (request *WorkflowMutableRequest) SetMutableID(value *string) {
	request.SetStringProperty("MutableId", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowMutableRequest) Clone() IProxyMessage {
	workflowMutableRequest := NewWorkflowMutableRequest()
	var messageClone IProxyMessage = workflowMutableRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowMutableRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowMutableRequest); ok {
		v.SetMutableID(request.GetMutableID())
	}
}
