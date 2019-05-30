package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowMutableInvokeRequest is WorkflowRequest of MessageType
	// WorkflowMutableInvokeRequest.
	//
	// A WorkflowMutableInvokeRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// A WorkflowMutableInvokeRequest will pass all of the given data
	// necessary to invoke a cadence workflow instance via the cadence client
	WorkflowMutableInvokeRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowMutableInvokeRequest is the default constructor for a WorkflowMutableInvokeRequest
//
// returns *WorkflowMutableInvokeRequest -> a reference to a newly initialized
// WorkflowMutableInvokeRequest in memory
func NewWorkflowMutableInvokeRequest() *WorkflowMutableInvokeRequest {
	request := new(WorkflowMutableInvokeRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(messagetypes.WorkflowMutableInvokeRequest)
	request.SetReplyType(messagetypes.WorkflowMutableInvokeReply)

	return request
}

// GetMutableID gets a WorkflowMutableInvokeRequest's MutableID value
// from its properties map. Identifies the mutable value to be returned.
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowMutableInvokeRequest's MutableID
func (request *WorkflowMutableInvokeRequest) GetMutableID() *string {
	return request.GetStringProperty("MutableId")
}

// SetMutableID sets an WorkflowMutableInvokeRequest's MutableID value
// in its properties map. Identifies the mutable value to be returned.
//
// param value *string -> pointer to a string in memory holding the value
// of a WorkflowMutableInvokeRequest's MutableID
func (request *WorkflowMutableInvokeRequest) SetMutableID(value *string) {
	request.SetStringProperty("MutableId", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowMutableInvokeRequest) Clone() IProxyMessage {
	workflowMutableInvokeRequest := NewWorkflowMutableInvokeRequest()
	var messageClone IProxyMessage = workflowMutableInvokeRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowMutableInvokeRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowMutableInvokeRequest); ok {
		v.SetMutableID(request.GetMutableID())
	}
}
