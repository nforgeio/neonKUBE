//-----------------------------------------------------------------------------
// FILE:		activity_execute_local_request.go
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

	// ActivityExecuteLocalRequest is an ActivityRequest of MessageType
	// ActivityExecuteLocalRequest.
	//
	// A ActivityExecuteLocalRequest contains a reference to a
	// ActivityRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ActivityRequest
	//
	// Starts a local workflow activity.
	ActivityExecuteLocalRequest struct {
		*ActivityRequest
	}
)

// NewActivityExecuteLocalRequest is the default constructor for a ActivityExecuteLocalRequest
//
// returns *ActivityExecuteLocalRequest -> a pointer to a newly initialized ActivityExecuteLocalRequest
// in memory
func NewActivityExecuteLocalRequest() *ActivityExecuteLocalRequest {
	request := new(ActivityExecuteLocalRequest)
	request.ActivityRequest = NewActivityRequest()
	request.SetType(internal.ActivityExecuteLocalRequest)
	request.SetReplyType(internal.ActivityExecuteLocalReply)

	return request
}

// GetActivityTypeID gets a ActivityExecuteLocalRequest's ActivityTypeID field
// from its properties map.  Identifies the .NET type that
// implements the local activity.
//
// returns int64 -> int64 representing the ActivityTypeID of the
// activity to be executed
func (request *ActivityExecuteLocalRequest) GetActivityTypeID() int64 {
	return request.GetLongProperty("ActivityTypeId")
}

// SetActivityTypeID sets an ActivityExecuteLocalRequest's ActivityTypeID field
// from its properties map.  Identifies the .NET type that
// implements the local activity.
//
// param value int64 -> int64 representing the ActivityTypeID of the
// activity to be executed
func (request *ActivityExecuteLocalRequest) SetActivityTypeID(value int64) {
	request.SetLongProperty("ActivityTypeId", value)
}

// GetArgs gets a ActivityExecuteLocalRequest's Args field
// from its properties map.  Optionally specifies the
// arguments to be passed to the activity encoded as a byte array.
//
// returns []byte -> []byte representing workflow activity parameters or arguments
// for executing
func (request *ActivityExecuteLocalRequest) GetArgs() []byte {
	return request.GetBytesProperty("Args")
}

// SetArgs sets an ActivityExecuteLocalRequest's Args field
// from its properties map.  Optionally specifies the
// arguments to be passed to the activity encoded as a byte array.
//
// param value []byte -> []byte representing workflow activity parameters or arguments
// for executing
func (request *ActivityExecuteLocalRequest) SetArgs(value []byte) {
	request.SetBytesProperty("Args", value)
}

// GetOptions gets a ActivityExecuteLocalRequest's local
// activity options.
//
// returns *workflow.LocalActivityOptions -> a temporal client struct that contains the
// options for executing a workflow activity
func (request *ActivityExecuteLocalRequest) GetOptions() *workflow.LocalActivityOptions {
	opts := new(workflow.LocalActivityOptions)
	err := request.GetJSONProperty("Options", opts)
	if err != nil {
		return nil
	}

	return opts
}

// SetOptions sets a ActivityExecuteLocalRequest's local
// activity options.
//
// param value *workflow.LocalActivityOptions -> a temporal client struct that contains the
// options for executing a workflow activity to be set in the ActivityExecuteLocalRequest's
// properties map
func (request *ActivityExecuteLocalRequest) SetOptions(value *workflow.LocalActivityOptions) {
	request.SetJSONProperty("Options", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ActivityRequest.Clone()
func (request *ActivityExecuteLocalRequest) Clone() IProxyMessage {
	activityExecuteLocalRequest := NewActivityExecuteLocalRequest()
	var messageClone IProxyMessage = activityExecuteLocalRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ActivityRequest.CopyTo()
func (request *ActivityExecuteLocalRequest) CopyTo(target IProxyMessage) {
	request.ActivityRequest.CopyTo(target)
	if v, ok := target.(*ActivityExecuteLocalRequest); ok {
		v.SetArgs(request.GetArgs())
		v.SetOptions(request.GetOptions())
		v.SetActivityTypeID(request.GetActivityTypeID())
	}
}
