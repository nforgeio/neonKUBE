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
	cadenceshared "go.uber.org/cadence/.gen/go/shared"

	internal "github.com/cadence-proxy/internal"
	proxyclient "github.com/cadence-proxy/internal/cadence/client"
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

// GetName gets the Name property from the DescribeTaskListRequest's
// properties map, identifies the task list.
//
// returns *string -> the task list name.
func (request *DescribeTaskListRequest) GetName() *string {
	return request.GetStringProperty("Name")
}

// SetName sets the Name property in the DescribeTaskListRequest's
// properties map, identifies the task list.
//
// param value *string -> the task list name.
func (request *DescribeTaskListRequest) SetName(value *string) {
	request.SetStringProperty("Name", value)
}

// GetDomain gets the Domain property from the DescribeTaskListRequest's
// properties map, identifies the target domain.
//
// returns *string -> the task list domain.
func (request *DescribeTaskListRequest) GetDomain() *string {
	return request.GetStringProperty("Domain")
}

// SetDomain sets the Domain property in the DescribeTaskListRequest's
// properties map, identifies the target domain.
//
// param value *string -> the task list domain.
func (request *DescribeTaskListRequest) SetDomain(value *string) {
	request.SetStringProperty("Domain", value)
}

// GetTaskListType gets the TaskListType property from the DescribeTaskListRequest's
// properties map, identifies the type of task list being requested:
// decision (AKA workflow) or activity.
//
// returns *cadenceshared.TaskListType -> the TaskListType.
func (request *DescribeTaskListRequest) GetTaskListType() *cadenceshared.TaskListType {
	taskListTypePtr := request.GetStringProperty("TaskListType")
	if taskListTypePtr == nil {
		return nil
	}

	return proxyclient.StringToTaskListType(*taskListTypePtr)
}

// SetTaskListType sets the TaskListType property in the DescribeTaskListRequest's
// properties map, identifies the type of task list being requested:
// decision (AKA workflow) or activity.
//
// param value cadenceshared.TaskListType -> the TaskListType.
func (request *DescribeTaskListRequest) SetTaskListType(value *cadenceshared.TaskListType) {
	taskListType := value.String()
	request.SetStringProperty("TaskListType", &taskListType)
}

// GetTaskListKind gets the TaskListKind property from the DescribeTaskListRequest's
// properties map, identifies the type of task list being requested:
// decision (AKA workflow) or activity.
//
// returns *cadenceshared.TaskListKind -> the TaskListKind.
func (request *DescribeTaskListRequest) GetTaskListKind() *cadenceshared.TaskListKind {
	taskListKindPtr := request.GetStringProperty("TaskListKind")
	if taskListKindPtr == nil {
		return nil
	}

	return proxyclient.StringToTaskListKind(*taskListKindPtr)
}

// SetTaskListKind sets the TaskListKind property in the DescribeTaskListRequest's
// properties map, identifies the type of task list being requested:
// decision (AKA workflow) or activity.
//
// param value cadenceshared.TaskListKind -> the TaskListKind.
func (request *DescribeTaskListRequest) SetTaskListKind(value *cadenceshared.TaskListKind) {
	taskListKind := value.String()
	request.SetStringProperty("TaskListKind", &taskListKind)
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
		v.SetName(request.GetName())
		v.SetDomain(request.GetDomain())
		v.SetTaskListType(request.GetTaskListType())
	}
}
