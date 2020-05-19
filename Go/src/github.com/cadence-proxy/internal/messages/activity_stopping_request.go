//-----------------------------------------------------------------------------
// FILE:		activity_stopping_request.go
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

	// ActivityStoppingRequest is an ActivityRequest of MessageType
	// ActivityStoppingRequest.
	//
	// A ActivityStoppingRequest contains a reference to a
	// ActivityRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ActivityRequest
	//
	// Sent to a worker, instructing it to stop executing
	// a workflow activity.
	ActivityStoppingRequest struct {
		*ActivityRequest
	}
)

// NewActivityStoppingRequest is the default constructor for a ActivityStoppingRequest
//
// returns *ActivityStoppingRequest -> a pointer to a newly initialized ActivityStoppingRequest
// in memory
func NewActivityStoppingRequest() *ActivityStoppingRequest {
	request := new(ActivityStoppingRequest)
	request.ActivityRequest = NewActivityRequest()
	request.SetType(internal.ActivityStoppingRequest)
	request.SetReplyType(internal.ActivityStoppingReply)

	return request
}

// GetActivityID gets a ActivityStoppingRequest's ActivityID field
// from its properties map.  Specifies the activity being stopped.
//
// returns *string -> pointer to string in memory holding
// the activityID of the activity to be stopped
func (request *ActivityStoppingRequest) GetActivityID() *string {
	return request.GetStringProperty("ActivityId")
}

// SetActivityID sets an ActivityStoppingRequest's ActivityID field
// from its properties map.  Specifies the activity being stopped.
//
// param value *string -> pointer to string in memory holding
// the activityID of the activity to be stopped
func (request *ActivityStoppingRequest) SetActivityID(value *string) {
	request.SetStringProperty("ActivityId", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ActivityRequest.Clone()
func (request *ActivityStoppingRequest) Clone() IProxyMessage {
	activityStoppingRequest := NewActivityStoppingRequest()
	var messageClone IProxyMessage = activityStoppingRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ActivityRequest.CopyTo()
func (request *ActivityStoppingRequest) CopyTo(target IProxyMessage) {
	request.ActivityRequest.CopyTo(target)
	if v, ok := target.(*ActivityStoppingRequest); ok {
		v.SetActivityID(request.GetActivityID())
	}
}
