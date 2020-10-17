//-----------------------------------------------------------------------------
// FILE:		describe_taskqueue_request.go
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
	"strings"

	internal "temporal-proxy/internal"

	enums "go.temporal.io/api/enums/v1"
)

type (

	// DescribeTaskQueueRequest is ProxyRequest of MessageType
	// DescribeTaskQueueRequest.
	//
	// A DescribeTaskQueueRequest contains a reference to a
	// ProxyRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ProxyRequest
	//
	// A DescribeTaskQueueRequest will pass all of the given data
	// necessary to describe a temporal workflow task queue
	DescribeTaskQueueRequest struct {
		*ProxyRequest
	}
)

// NewDescribeTaskQueueRequest is the default constructor for a DescribeTaskQueueRequest
//
// returns *DescribeTaskQueueRequest -> a reference to a newly initialized
// DescribeTaskQueueRequest in memory
func NewDescribeTaskQueueRequest() *DescribeTaskQueueRequest {
	request := new(DescribeTaskQueueRequest)
	request.ProxyRequest = NewProxyRequest()
	request.SetType(internal.DescribeTaskQueueRequest)
	request.SetReplyType(internal.DescribeTaskQueueReply)

	return request
}

// GetName gets the Name property from the DescribeTaskQueueRequest's
// properties map, identifies the task queue.
//
// returns *string -> the task queue name.
func (request *DescribeTaskQueueRequest) GetName() *string {
	return request.GetStringProperty("Name")
}

// SetName sets the Name property in the DescribeTaskQueueRequest's
// properties map, identifies the task queue.
//
// param value *string -> the task queue name.
func (request *DescribeTaskQueueRequest) SetName(value *string) {
	request.SetStringProperty("Name", value)
}

// GetNamespace gets the Namespace property from the DescribeTaskQueueRequest's
// properties map, identifies the target namespace.
//
// returns *string -> the task queue namespace.
func (request *DescribeTaskQueueRequest) GetNamespace() *string {
	return request.GetStringProperty("Namespace")
}

// SetNamespace sets the Namespace property in the DescribeTaskQueueRequest's
// properties map, identifies the target namespace.
//
// param value *string -> the task queue namespace.
func (request *DescribeTaskQueueRequest) SetNamespace(value *string) {
	request.SetStringProperty("Namespace", value)
}

// GetTaskQueueType gets the TaskQueueType property from the DescribeTaskQueueRequest's
// properties map, identifies the type of task queue being requested:
// decision (AKA workflow) or activity.
//
// returns enums.TaskQueueType -> the TaskQueueType.
func (request *DescribeTaskQueueRequest) GetTaskQueueType() enums.TaskQueueType {
	taskQueueTypePtr := request.GetStringProperty("TaskQueueType")
	if taskQueueTypePtr == nil {
		return enums.TASK_QUEUE_TYPE_WORKFLOW
	}

	return StringToTaskQueueType(*taskQueueTypePtr)
}

// SetTaskQueueType sets the TaskQueueType property in the DescribeTaskQueueRequest's
// properties map, identifies the type of task queue being requested:
// decision (AKA workflow) or activity.
//
// param value workflowservice.TaskQueueType -> the TaskQueueType.
func (request *DescribeTaskQueueRequest) SetTaskQueueType(value enums.TaskQueueType) {
	taskQueueType := value.String()
	request.SetStringProperty("TaskQueueType", &taskQueueType)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyRequest.Clone()
func (request *DescribeTaskQueueRequest) Clone() IProxyMessage {
	workflowDescribeTaskQueueRequest := NewDescribeTaskQueueRequest()
	var messageClone IProxyMessage = workflowDescribeTaskQueueRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyRequest.CopyTo()
func (request *DescribeTaskQueueRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
	if v, ok := target.(*DescribeTaskQueueRequest); ok {
		v.SetName(request.GetName())
		v.SetNamespace(request.GetNamespace())
		v.SetTaskQueueType(request.GetTaskQueueType())
	}
}

// -------------------------------------------------------------------------
// Helper methods

// StringToTaskQueueType takes a valid TaskQueueType
// as a string and converts it into a TaskQueueType
func StringToTaskQueueType(value string) enums.TaskQueueType {
	value = strings.ToUpper(value)
	switch value {
	case "WORKFLOW":
		return enums.TASK_QUEUE_TYPE_WORKFLOW
	case "ACTIVITY":
		return enums.TASK_QUEUE_TYPE_ACTIVITY
	default:
		return enums.TASK_QUEUE_TYPE_UNSPECIFIED
	}
}

// StringToTaskQueueKind takes a valid TaskQueueKind
// as a string and converts it into a TaskQueueKind
func StringToTaskQueueKind(value string) enums.TaskQueueKind {
	value = strings.ToUpper(value)
	switch value {
	case "NORMAL":
		return enums.TASK_QUEUE_KIND_NORMAL
	case "STICKY":
		return enums.TASK_QUEUE_KIND_STICKY
	default:
		return enums.TASK_QUEUE_KIND_UNSPECIFIED
	}
}
