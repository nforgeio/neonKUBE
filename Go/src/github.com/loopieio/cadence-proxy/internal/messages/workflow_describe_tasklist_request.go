package messages

import (
	"fmt"
	"time"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowDescribeTaskListRequest is WorkflowRequest of MessageType
	// WorkflowDescribeTaskListRequest.
	//
	// A WorkflowDescribeTaskListRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// A WorkflowDescribeTaskListRequest will pass all of the given data
	// necessary to describe a cadence workflow task list
	WorkflowDescribeTaskListRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowDescribeTaskListRequest is the default constructor for a WorkflowDescribeTaskListRequest
//
// returns *WorkflowDescribeTaskListRequest -> a reference to a newly initialized
// WorkflowDescribeTaskListRequest in memory
func NewWorkflowDescribeTaskListRequest() *WorkflowDescribeTaskListRequest {
	request := new(WorkflowDescribeTaskListRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(messagetypes.WorkflowDescribeTaskListRequest)
	request.SetReplyType(messagetypes.WorkflowDescribeTaskListReply)

	return request
}

// GetTaskList gets the TaskList property from the WorkflowDescribeTaskListRequest's
// properties map.  The TaskList property specifies the cadence tasklist to
// be described.
//
// returns *string -> pointer to the string in memory holding the value of
// the TaskList property in the WorkflowDescribeTaskListRequest.
func (request *WorkflowDescribeTaskListRequest) GetTaskList() *string {
	return request.GetStringProperty("TaskList")
}

// SetTaskList sets the TaskList property in the WorkflowDescribeTaskListRequest's
// properties map.  The TaskList property specifies the cadence tasklist to
// be described.
//
// param value *string -> pointer to the string in memory holding the value of
// the TaskList property in the WorkflowDescribeTaskListRequest.
func (request *WorkflowDescribeTaskListRequest) SetTaskList(value *string) {
	request.SetStringProperty("TaskList", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowDescribeTaskListRequest) Clone() IProxyMessage {
	workflowDescribeTaskListRequest := NewWorkflowDescribeTaskListRequest()
	var messageClone IProxyMessage = workflowDescribeTaskListRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowDescribeTaskListRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowDescribeTaskListRequest); ok {
		fmt.Printf("TODO: JACK -- IMPLEMENT %v", v)
	}
}

// SetProxyMessage inherits docs from WorkflowRequest.SetProxyMessage()
func (request *WorkflowDescribeTaskListRequest) SetProxyMessage(value *ProxyMessage) {
	request.WorkflowRequest.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from WorkflowRequest.GetProxyMessage()
func (request *WorkflowDescribeTaskListRequest) GetProxyMessage() *ProxyMessage {
	return request.WorkflowRequest.GetProxyMessage()
}

// GetRequestID inherits docs from WorkflowRequest.GetRequestID()
func (request *WorkflowDescribeTaskListRequest) GetRequestID() int64 {
	return request.WorkflowRequest.GetRequestID()
}

// SetRequestID inherits docs from WorkflowRequest.SetRequestID()
func (request *WorkflowDescribeTaskListRequest) SetRequestID(value int64) {
	request.WorkflowRequest.SetRequestID(value)
}

// GetType inherits docs from WorkflowRequest.GetType()
func (request *WorkflowDescribeTaskListRequest) GetType() messagetypes.MessageType {
	return request.WorkflowRequest.GetType()
}

// SetType inherits docs from WorkflowRequest.SetType()
func (request *WorkflowDescribeTaskListRequest) SetType(value messagetypes.MessageType) {
	request.WorkflowRequest.SetType(value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType inherits docs from WorkflowRequest.GetReplyType()
func (request *WorkflowDescribeTaskListRequest) GetReplyType() messagetypes.MessageType {
	return request.WorkflowRequest.GetReplyType()
}

// SetReplyType inherits docs from WorkflowRequest.SetReplyType()
func (request *WorkflowDescribeTaskListRequest) SetReplyType(value messagetypes.MessageType) {
	request.WorkflowRequest.SetReplyType(value)
}

// GetTimeout inherits docs from WorkflowRequest.GetTimeout()
func (request *WorkflowDescribeTaskListRequest) GetTimeout() time.Duration {
	return request.WorkflowRequest.GetTimeout()
}

// SetTimeout inherits docs from WorkflowRequest.SetTimeout()
func (request *WorkflowDescribeTaskListRequest) SetTimeout(value time.Duration) {
	request.WorkflowRequest.SetTimeout(value)
}

// -------------------------------------------------------------------------
// IWorkflowRequest interface methods for implementing the IWorkflowRequest interface

// GetContextID inherits docs from WorkflowRequest.GetContextID()
func (request *WorkflowDescribeTaskListRequest) GetContextID() int64 {
	return request.WorkflowRequest.GetContextID()
}

// SetContextID inherits docs from WorkflowRequest.GetContextID()
func (request *WorkflowDescribeTaskListRequest) SetContextID(value int64) {
	request.WorkflowRequest.SetContextID(value)
}
