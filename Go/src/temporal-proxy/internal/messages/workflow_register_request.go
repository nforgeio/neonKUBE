//-----------------------------------------------------------------------------
// FILE:		workflow_register_request.go
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
	internal "temporal-proxy/internal"
)

type (

	// WorkflowRegisterRequest is WorkflowRequest of MessageType
	// WorkflowRegisterRequest.
	//
	// A WorkflowRegisterRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// A WorkflowRegisterRequest will pass all of the given information
	// necessary to register a workflow function with the temporal server
	// via the temporal client
	WorkflowRegisterRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowRegisterRequest is the default constructor for a WorkflowRegisterRequest
//
// returns *WorkflowRegisterRequest -> a reference to a newly initialized
// WorkflowRegisterRequest in memory
func NewWorkflowRegisterRequest() *WorkflowRegisterRequest {
	request := new(WorkflowRegisterRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(internal.WorkflowRegisterRequest)
	request.SetReplyType(internal.WorkflowRegisterReply)

	return request
}

// GetName gets a WorkflowRegisterRequest's Name value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowRegisterRequest's Name
func (request *WorkflowRegisterRequest) GetName() *string {
	return request.GetStringProperty("Name")
}

// SetName sets a WorkflowRegisterRequest's Name value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowRegisterRequest) SetName(value *string) {
	request.SetStringProperty("Name", value)
}

// GetWorkerID gets a WorkflowRegisterRequest's WorkerID value
// from its properties map
//
// returns int64 -> long Id of the worker to register the workflow.
func (request *WorkflowRegisterRequest) GetWorkerID() int64 {
	return request.GetLongProperty("WorkerId")
}

// SetWorkerID sets a WorkflowRegisterRequest's WorkerID value
// in its properties map.
//
// param value int64 -> long Id of the worker to register the workflow.
func (request *WorkflowRegisterRequest) SetWorkerID(value int64) {
	request.SetLongProperty("WorkerId", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowRegisterRequest) Clone() IProxyMessage {
	workflowRegisterRequest := NewWorkflowRegisterRequest()
	var messageClone IProxyMessage = workflowRegisterRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowRegisterRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowRegisterRequest); ok {
		v.SetName(request.GetName())
		v.SetWorkerID(request.GetWorkerID())
	}
}
