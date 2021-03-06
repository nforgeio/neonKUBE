//-----------------------------------------------------------------------------
// FILE:		activity_get_local_result_request.go
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

	// ActivityGetLocalResultRequest is an ActivityRequest of MessageType
	// ActivityGetLocalResultRequest.
	//
	// A ActivityGetLocalResultRequest contains a reference to a
	// ActivityRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ActivityRequest
	//
	// Starts a workflow activity.
	ActivityGetLocalResultRequest struct {
		*ActivityRequest
	}
)

// NewActivityGetLocalResultRequest is the default constructor for a ActivityGetLocalResultRequest
//
// returns *ActivityGetLocalResultRequest -> a pointer to a newly initialized ActivityGetLocalResultRequest
// in memory
func NewActivityGetLocalResultRequest() *ActivityGetLocalResultRequest {
	request := new(ActivityGetLocalResultRequest)
	request.ActivityRequest = NewActivityRequest()
	request.SetType(internal.ActivityGetLocalResultRequest)
	request.SetReplyType(internal.ActivityGetLocalResultReply)

	return request
}

// GetActivityID gets the unique Id used to identify the activity.
//
// returns int64 -> the long ActivityID
func (request *ActivityGetLocalResultRequest) GetActivityID() int64 {
	return request.GetLongProperty("ActivityId")
}

// SetActivityID sets the int64 Id used to identify the activity.
//
// param value int64 -> the long Id to set as the ActivityID.
func (request *ActivityGetLocalResultRequest) SetActivityID(value int64) {
	request.SetLongProperty("ActivityId", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ActivityRequest.Clone()
func (request *ActivityGetLocalResultRequest) Clone() IProxyMessage {
	activityGetLocalResultRequest := NewActivityGetLocalResultRequest()
	var messageClone IProxyMessage = activityGetLocalResultRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ActivityRequest.CopyTo()
func (request *ActivityGetLocalResultRequest) CopyTo(target IProxyMessage) {
	request.ActivityRequest.CopyTo(target)
	if v, ok := target.(*ActivityGetLocalResultRequest); ok {
		v.SetActivityID(request.GetActivityID())
	}
}
