//-----------------------------------------------------------------------------
// FILE:		workflow_sleep_request.go
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
	"time"

	internal "temporal-proxy/internal"
)

type (

	// WorkflowSleepRequest is WorkflowRequest of MessageType
	// WorkflowSleepRequest.
	//
	// A WorkflowSleepRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// Commands the workflow to sleep for a period of time.
	WorkflowSleepRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowSleepRequest is the default constructor for a WorkflowSleepRequest
//
// returns *WorkflowSleepRequest -> a reference to a newly initialized
// WorkflowSleepRequest in memory
func NewWorkflowSleepRequest() *WorkflowSleepRequest {
	request := new(WorkflowSleepRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(internal.WorkflowSleepRequest)
	request.SetReplyType(internal.WorkflowSleepReply)

	return request
}

// GetDuration gets the Duration property from the WorkflowSleepRequest's
// properties map. Duration specifies the time to sleep.
//
// returns time.Duration -> the value of the Duration property from
// the WorkflowSleepRequest's properties map.
func (request *WorkflowSleepRequest) GetDuration() time.Duration {
	return request.GetTimeSpanProperty("Duration")
}

// SetDuration sets the Duration property in the WorkflowSleepRequest's
// properties map. Duration specifies the time to sleep.
//
// param value time.Duration -> the time.Duration to be set in the
// WorkflowSleepRequest's properties map.
func (request *WorkflowSleepRequest) SetDuration(value time.Duration) {
	request.SetTimeSpanProperty("Duration", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowSleepRequest) Clone() IProxyMessage {
	workflowSleepRequest := NewWorkflowSleepRequest()
	var messageClone IProxyMessage = workflowSleepRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowSleepRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowSleepRequest); ok {
		v.SetDuration(request.GetDuration())
	}
}
