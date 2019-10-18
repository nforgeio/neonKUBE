//-----------------------------------------------------------------------------
// FILE:		describe_tasklist_request.go
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
	internal "github.com/cadence-proxy/internal"
)

type (

	// DescribeTaskListRequest is ProxyRequest of MessageType
	// DescribeTaskListRequest.
	//
	// A DescribeTaskListRequest contains a reference to a
	// ProxyRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ProxyRequest
	//
	// A DescribeTaskListRequest will pass all of the given data
	// necessary to describe a cadence workflow task list
	DescribeTaskListRequest struct {
		*ProxyRequest
	}
)

// NewDescribeTaskListRequest is the default constructor for a DescribeTaskListRequest
//
// returns *DescribeTaskListRequest -> a reference to a newly initialized
// DescribeTaskListRequest in memory
func NewDescribeTaskListRequest() *DescribeTaskListRequest {
	request := new(DescribeTaskListRequest)
	request.ProxyRequest = NewProxyRequest()
	request.SetType(internal.DescribeTaskListRequest)
	request.SetReplyType(internal.DescribeTaskListReply)

	return request
}

// GetTaskList gets the TaskList property from the DescribeTaskListRequest's
// properties map.  The TaskList property specifies the cadence tasklist to
// be described.
//
// returns *string -> pointer to the string in memory holding the value of
// the TaskList property in the DescribeTaskListRequest.
func (request *DescribeTaskListRequest) GetTaskList() *string {
	return request.GetStringProperty("TaskList")
}

// SetTaskList sets the TaskList property in the DescribeTaskListRequest's
// properties map.  The TaskList property specifies the cadence tasklist to
// be described.
//
// param value *string -> pointer to the string in memory holding the value of
// the TaskList property in the DescribeTaskListRequest.
func (request *DescribeTaskListRequest) SetTaskList(value *string) {
	request.SetStringProperty("TaskList", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyRequest.Clone()
func (request *DescribeTaskListRequest) Clone() IProxyMessage {
	workflowDescribeTaskListRequest := NewDescribeTaskListRequest()
	var messageClone IProxyMessage = workflowDescribeTaskListRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyRequest.CopyTo()
func (request *DescribeTaskListRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
	if v, ok := target.(*DescribeTaskListRequest); ok {
		v.SetTaskList(request.GetTaskList())
	}
}
