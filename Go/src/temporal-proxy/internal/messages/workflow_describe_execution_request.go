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
	internal "temporal-proxy/internal"
)

type (

	// WorkflowDescribeExecutionRequest is WorkflowRequest of MessageType
	// WorkflowDescribeExecutionRequest.
	//
	// A WorkflowDescribeExecutionRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest.
	//
	// Describes an executing workflow instance.
	WorkflowDescribeExecutionRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowDescribeExecutionRequest is the default constructor for a WorkflowDescribeExecutionRequest.
//
// returns *WorkflowDescribeExecutionRequest -> a reference to a newly initialized
// WorkflowDescribeExecutionRequest in memory.
func NewWorkflowDescribeExecutionRequest() *WorkflowDescribeExecutionRequest {
	request := new(WorkflowDescribeExecutionRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(internal.WorkflowDescribeExecutionRequest)
	request.SetReplyType(internal.WorkflowDescribeExecutionReply)

	return request
}

// GetWorkflowID gets a WorkflowDescribeExecutionRequest's WorkflowID value
// from its properties map, identifies the workflow by ID.
//
// returns *string -> WorkflowDescribeExecutionRequest's string WorkflowID.
func (request *WorkflowDescribeExecutionRequest) GetWorkflowID() *string {
	return request.GetStringProperty("WorkflowId")
}

// SetWorkflowID sets an WorkflowDescribeExecutionRequest's WorkflowID value
// in its properties map, identifies the workflow by ID.
//
// param value *string -> WorkflowDescribeExecutionRequest's string WorkflowID.
func (request *WorkflowDescribeExecutionRequest) SetWorkflowID(value *string) {
	request.SetStringProperty("WorkflowId", value)
}

// GetRunID gets a WorkflowDescribeExecutionRequest's RunID value
// from its properties map, identifies the specific workflow execution to be cancelled
// and the latest run will be cancelled when this is nil or empty.
//
// returns *string -> WorkflowDescribeExecutionRequest's string RunID.
func (request *WorkflowDescribeExecutionRequest) GetRunID() *string {
	return request.GetStringProperty("RunId")
}

// SetRunID sets a WorkflowDescribeExecutionRequest's RunID value
// in its properties map, identifies the specific workflow execution to be cancelled
// and the latest run will be cancelled when this is nil or empty.
//
// param value *string -> WorkflowDescribeExecutionRequest's string RunID.
func (request *WorkflowDescribeExecutionRequest) SetRunID(value *string) {
	request.SetStringProperty("RunId", value)
}

// GetNamespace gets a WorkflowDescribeExecutionRequest's Namespace value
// from its properties map, optionally identifies the target namespace when
// RunIdisn't passed.
//
// returns *string -> WorkflowDescribeExecutionRequest's string Namespace.
func (request *WorkflowDescribeExecutionRequest) GetNamespace() *string {
	return request.GetStringProperty("Namespace")
}

// SetNamespace sets a WorkflowDescribeExecutionRequest's Namespace value
// in its properties map, optionally identifies the target namespace when
// RunIdisn't passed.
//
// param value *string -> WorkflowDescribeExecutionRequest's string Namespace.
func (request *WorkflowDescribeExecutionRequest) SetNamespace(value *string) {
	request.SetStringProperty("Namespace", value)
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
		v.SetNamespace(request.GetNamespace())
	}
}
