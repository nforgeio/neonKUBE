//-----------------------------------------------------------------------------
// FILE:		workflow_describe_execution_request.go
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
	request.SetType(internal.WorkflowDescribeExecutionRequest)
	request.SetReplyType(internal.WorkflowDescribeExecutionReply)

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

// GetDomain gets a WorkflowDescribeExecutionRequest's Domain value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowDescribeExecutionRequest's Domain
func (request *WorkflowDescribeExecutionRequest) GetDomain() *string {
	return request.GetStringProperty("Domain")
}

// SetDomain sets a WorkflowDescribeExecutionRequest's Domain value
// in its properties map.
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowDescribeExecutionRequest) SetDomain(value *string) {
	request.SetStringProperty("Domain", value)
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
		v.SetDomain(request.GetDomain())
	}
}
