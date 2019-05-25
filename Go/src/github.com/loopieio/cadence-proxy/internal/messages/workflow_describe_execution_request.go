package messages

import (
	"time"

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

// SetProxyMessage inherits docs from WorkflowRequest.SetProxyMessage()
func (request *WorkflowDescribeExecutionRequest) SetProxyMessage(value *ProxyMessage) {
	request.WorkflowRequest.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from WorkflowRequest.GetProxyMessage()
func (request *WorkflowDescribeExecutionRequest) GetProxyMessage() *ProxyMessage {
	return request.WorkflowRequest.GetProxyMessage()
}

// GetRequestID inherits docs from WorkflowRequest.GetRequestID()
func (request *WorkflowDescribeExecutionRequest) GetRequestID() int64 {
	return request.WorkflowRequest.GetRequestID()
}

// SetRequestID inherits docs from WorkflowRequest.SetRequestID()
func (request *WorkflowDescribeExecutionRequest) SetRequestID(value int64) {
	request.WorkflowRequest.SetRequestID(value)
}

// GetType inherits docs from WorkflowRequest.GetType()
func (request *WorkflowDescribeExecutionRequest) GetType() messagetypes.MessageType {
	return request.WorkflowRequest.GetType()
}

// SetType inherits docs from WorkflowRequest.SetType()
func (request *WorkflowDescribeExecutionRequest) SetType(value messagetypes.MessageType) {
	request.WorkflowRequest.SetType(value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType inherits docs from WorkflowRequest.GetReplyType()
func (request *WorkflowDescribeExecutionRequest) GetReplyType() messagetypes.MessageType {
	return request.WorkflowRequest.GetReplyType()
}

// SetReplyType inherits docs from WorkflowRequest.SetReplyType()
func (request *WorkflowDescribeExecutionRequest) SetReplyType(value messagetypes.MessageType) {
	request.WorkflowRequest.SetReplyType(value)
}

// GetTimeout inherits docs from WorkflowRequest.GetTimeout()
func (request *WorkflowDescribeExecutionRequest) GetTimeout() time.Duration {
	return request.WorkflowRequest.GetTimeout()
}

// SetTimeout inherits docs from WorkflowRequest.SetTimeout()
func (request *WorkflowDescribeExecutionRequest) SetTimeout(value time.Duration) {
	request.WorkflowRequest.SetTimeout(value)
}

// -------------------------------------------------------------------------
// IWorkflowRequest interface methods for implementing the IWorkflowRequest interface

// GetWorkflowContextID inherits docs from WorkflowRequest.GetWorkflowContextID()
func (request *WorkflowDescribeExecutionRequest) GetWorkflowContextID() int64 {
	return request.WorkflowRequest.GetWorkflowContextID()
}

// SetWorkflowContextID inherits docs from WorkflowRequest.GetWorkflowContextID()
func (request *WorkflowDescribeExecutionRequest) SetWorkflowContextID(value int64) {
	request.WorkflowRequest.SetWorkflowContextID(value)
}
