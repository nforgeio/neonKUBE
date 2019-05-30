package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowDescribeExecutionRequest is WorkflowRequest of MessageType
	// WorkflowDescribeExecutionRequest.
	//
	// A WorkflowDescribeExecutionRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// A WorkflowDescribeExecutionRequest will pass all of the given data
	// necessary to describe the execution of a cadence workflow instance
	WorkflowDescribeExecutionRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowDescribeExecutionRequest is the default constructor for a WorkflowDescribeExecutionRequest
//
// returns *WorkflowDescribeExecutionRequest -> a reference to a newly initialized
// WorkflowDescribeExecutionRequest in memory
func NewWorkflowDescribeExecutionRequest() *WorkflowDescribeExecutionRequest {
	request := new(WorkflowDescribeExecutionRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(messagetypes.WorkflowDescribeExecutionRequest)
	request.SetReplyType(messagetypes.WorkflowDescribeExecutionReply)

	return request
}

// GetWorkflowID gets a WorkflowDescribeExecutionRequest's WorkflowID value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowDescribeExecutionRequest's WorkflowID
func (request *WorkflowDescribeExecutionRequest) GetWorkflowID() *string {
	return request.GetStringProperty("WorkflowId")
}

// SetWorkflowID sets an WorkflowDescribeExecutionRequest's WorkflowID value
// in its properties map
//
// param value *string -> pointer to a string in memory holding the value
// of a WorkflowDescribeExecutionRequest's WorkflowID
func (request *WorkflowDescribeExecutionRequest) SetWorkflowID(value *string) {
	request.SetStringProperty("WorkflowId", value)
}

// GetRunID gets a WorkflowDescribeExecutionRequest's RunID value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowDescribeExecutionRequest's RunID
func (request *WorkflowDescribeExecutionRequest) GetRunID() *string {
	return request.GetStringProperty("RunId")
}

// SetRunID sets a WorkflowDescribeExecutionRequest's RunID value
// in its properties map.
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowDescribeExecutionRequest) SetRunID(value *string) {
	request.SetStringProperty("RunId", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowDescribeExecutionRequest) Clone() IProxyMessage {
	workflowDescribeExecutionRequest := NewWorkflowDescribeExecutionRequest()
	var messageClone IProxyMessage = workflowDescribeExecutionRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowDescribeExecutionRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowDescribeExecutionRequest); ok {
		v.SetWorkflowID(request.GetWorkflowID())
		v.SetRunID(request.GetRunID())
	}
}
