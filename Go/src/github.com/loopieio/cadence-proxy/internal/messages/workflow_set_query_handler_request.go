package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowSetQueryHandlerRequest is WorkflowRequest of MessageType
	// WorkflowSetQueryHandlerRequest.
	//
	// A WorkflowSetQueryHandlerRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// Sets a handler for a cadence workflow query
	WorkflowSetQueryHandlerRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowSetQueryHandlerRequest is the default constructor for a WorkflowSetQueryHandlerRequest
//
// returns *WorkflowSetQueryHandlerRequest -> a reference to a newly initialized
// WorkflowSetQueryHandlerRequest in memory
func NewWorkflowSetQueryHandlerRequest() *WorkflowSetQueryHandlerRequest {
	request := new(WorkflowSetQueryHandlerRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(messagetypes.WorkflowSetQueryHandlerRequest)
	request.SetReplyType(messagetypes.WorkflowQueryReply)

	return request
}

// GetQueryName gets a WorkflowSetQueryHandlerRequest's QueryName value
// from its properties map. Identifies the query by name.
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowSetQueryHandlerRequest's QueryName
func (request *WorkflowSetQueryHandlerRequest) GetQueryName() *string {
	return request.GetStringProperty("QueryName")
}

// SetQueryName sets a WorkflowSetQueryHandlerRequest's QueryName value
// in its properties map.  Identifies the query by name.
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowSetQueryHandlerRequest) SetQueryName(value *string) {
	request.SetStringProperty("QueryName", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowSetQueryHandlerRequest) Clone() IProxyMessage {
	workflowSetQueryHandlerRequest := NewWorkflowSetQueryHandlerRequest()
	var messageClone IProxyMessage = workflowSetQueryHandlerRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowSetQueryHandlerRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowSetQueryHandlerRequest); ok {
		v.SetQueryName(request.GetQueryName())
	}
}
