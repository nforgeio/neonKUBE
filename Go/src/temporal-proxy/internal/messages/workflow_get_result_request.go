//-----------------------------------------------------------------------------
// FILE:		workflow_get_result_request.go
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

	// WorkflowGetResultRequest is WorkflowRequest of MessageType
	// WorkflowGetResultRequest.
	//
	// A WorkflowGetResultRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// A WorkflowGetResultRequest will pass all of the given data
	// necessary to get the execution result of a temporal workflow instance
	WorkflowGetResultRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowGetResultRequest is the default constructor for a WorkflowGetResultRequest
//
// returns *WorkflowGetResultRequest -> a reference to a newly initialized
// WorkflowGetResultRequest in memory
func NewWorkflowGetResultRequest() *WorkflowGetResultRequest {
	request := new(WorkflowGetResultRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(internal.WorkflowGetResultRequest)
	request.SetReplyType(internal.WorkflowGetResultReply)

	return request
}

// GetWorkflowID gets a WorkflowGetResultRequest's WorkflowID value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowGetResultRequest's WorkflowID
func (request *WorkflowGetResultRequest) GetWorkflowID() *string {
	return request.GetStringProperty("WorkflowId")
}

// SetWorkflowID sets an WorkflowGetResultRequest's WorkflowID value
// in its properties map
//
// param value *string -> pointer to a string in memory holding the value
// of a WorkflowGetResultRequest's WorkflowID
func (request *WorkflowGetResultRequest) SetWorkflowID(value *string) {
	request.SetStringProperty("WorkflowId", value)
}

// GetRunID gets a WorkflowGetResultRequest's RunID value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowGetResultRequest's RunID
func (request *WorkflowGetResultRequest) GetRunID() *string {
	return request.GetStringProperty("RunId")
}

// SetRunID sets a WorkflowGetResultRequest's RunID value
// in its properties map.
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowGetResultRequest) SetRunID(value *string) {
	request.SetStringProperty("RunId", value)
}

// GetNamespace gets a WorkflowGetResultRequest's Namespace value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowGetResultRequest's Namespace
func (request *WorkflowGetResultRequest) GetNamespace() *string {
	return request.GetStringProperty("Namespace")
}

// SetNamespace sets a WorkflowGetResultRequest's Namespace value
// in its properties map.
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowGetResultRequest) SetNamespace(value *string) {
	request.SetStringProperty("Namespace", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowGetResultRequest) Clone() IProxyMessage {
	workflowGetResultRequest := NewWorkflowGetResultRequest()
	var messageClone IProxyMessage = workflowGetResultRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowGetResultRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowGetResultRequest); ok {
		v.SetWorkflowID(request.GetWorkflowID())
		v.SetRunID(request.GetRunID())
		v.SetNamespace(request.GetNamespace())
	}
}
