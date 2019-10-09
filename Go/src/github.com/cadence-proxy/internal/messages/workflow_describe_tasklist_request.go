//-----------------------------------------------------------------------------
// FILE:		workflow_describe_tasklist_request.go
// CONTRIBUTOR: John C Burns
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

package messages

import (
	"fmt"

	internal "github.com/cadence-proxy/internal"
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
	request.SetType(internal.WorkflowDescribeTaskListRequest)
	request.SetReplyType(internal.WorkflowDescribeTaskListReply)

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
