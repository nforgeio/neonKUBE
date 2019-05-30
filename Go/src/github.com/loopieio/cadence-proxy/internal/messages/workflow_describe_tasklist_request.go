package messages

import (
	"fmt"

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
