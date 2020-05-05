//-----------------------------------------------------------------------------
// FILE:		activity_start_request.go
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
	"go.temporal.io/temporal/workflow"

	internal "temporal-proxy/internal"
)

type (

	// ActivityStartRequest is an WorkflowRequest of MessageType
	// ActivityStartRequest.
	//
	// A ActivityStartRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// Starts a workflow activity.
	ActivityStartRequest struct {
		*WorkflowRequest
	}
)

// NewActivityStartRequest is the default constructor for a ActivityStartRequest
//
// returns *ActivityStartRequest -> a pointer to a newly initialized ActivityStartRequest
// in memory
func NewActivityStartRequest() *ActivityStartRequest {
	request := new(ActivityStartRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(internal.ActivityStartRequest)
	request.SetReplyType(internal.ActivityStartReply)

	return request
}

// GetActivity gets a ActivityStartRequest's Activity field
// from its properties map.  Specifies the activity to
// be executed.
//
// returns *string -> activity to execute.
func (request *ActivityStartRequest) GetActivity() *string {
	return request.GetStringProperty("Activity")
}

// SetActivity sets an ActivityStartRequest's Activity field
// from its properties map.  Specifies the activity to
// be executed.
//
// param value *string -> activity to execute.
func (request *ActivityStartRequest) SetActivity(value *string) {
	request.SetStringProperty("Activity", value)
}

// GetArgs gets a ActivityStartRequest's Args field
// from its properties map.  Args is a []byte that hold the arguments
// for executing a specific workflow activity.
//
// returns []byte -> []byte representing workflow activity parameters or arguments
// for executing
func (request *ActivityStartRequest) GetArgs() []byte {
	return request.GetBytesProperty("Args")
}

// SetArgs sets an ActivityStartRequest's Args field
// from its properties map.  Args is a []byte that hold the arguments
// for executing a specific workflow activity
//
// param value []byte -> []byte representing workflow activity parameters or arguments
// for executing
func (request *ActivityStartRequest) SetArgs(value []byte) {
	request.SetBytesProperty("Args", value)
}

// GetOptions gets a ActivityExecutionRequest's execution options.
//
// returns *workflow.ActivityOptions -> activity options.
func (request *ActivityStartRequest) GetOptions() *workflow.ActivityOptions {
	opts := new(workflow.ActivityOptions)
	err := request.GetJSONProperty("Options", opts)
	if err != nil {
		return nil
	}

	return opts
}

// SetOptions sets a ActivityExecutionRequest's execution options.
//
// param value *workflow.ActivityOptions -> activity options.
func (request *ActivityStartRequest) SetOptions(value *workflow.ActivityOptions) {
	request.SetJSONProperty("Options", value)
}

// GetNamespace gets a ActivityStartRequest's Namespace value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a ActivityStartRequest's Namespace
func (request *ActivityStartRequest) GetNamespace() *string {
	return request.GetStringProperty("Namespace")
}

// SetNamespace sets a ActivityStartRequest's Namespace value
// in its properties map.
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *ActivityStartRequest) SetNamespace(value *string) {
	request.SetStringProperty("Namespace", value)
}

// GetActivityID gets the unique Id used to identify the activity.
//
// returns int64 -> the long ActivityID
func (request *ActivityStartRequest) GetActivityID() int64 {
	return request.GetLongProperty("ActivityId")
}

// SetActivityID sets the int64 Id used to identify the activity.
//
// param value int64 -> the long Id to set as the ActivityID.
func (request *ActivityStartRequest) SetActivityID(value int64) {
	request.SetLongProperty("ActivityId", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *ActivityStartRequest) Clone() IProxyMessage {
	activityStartRequest := NewActivityStartRequest()
	var messageClone IProxyMessage = activityStartRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *ActivityStartRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*ActivityStartRequest); ok {
		v.SetArgs(request.GetArgs())
		v.SetOptions(request.GetOptions())
		v.SetActivity(request.GetActivity())
		v.SetNamespace(request.GetNamespace())
		v.SetActivityID(request.GetActivityID())
	}
}
