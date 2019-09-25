//-----------------------------------------------------------------------------
// FILE:		activity_get_result_request.go
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
	messagetypes "github.com/cadence-proxy/internal/messages/types"
)

type (

	// ActivityGetResultRequest is an ActivityRequest of MessageType
	// ActivityGetResultRequest.
	//
	// A ActivityGetResultRequest contains a reference to a
	// ActivityRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ActivityRequest
	//
	// Starts a workflow activity.
	ActivityGetResultRequest struct {
		*ActivityRequest
	}
)

// NewActivityGetResultRequest is the default constructor for a ActivityGetResultRequest
//
// returns *ActivityGetResultRequest -> a pointer to a newly initialized ActivityGetResultRequest
// in memory
func NewActivityGetResultRequest() *ActivityGetResultRequest {
	request := new(ActivityGetResultRequest)
	request.ActivityRequest = NewActivityRequest()
	request.SetType(messagetypes.ActivityGetResultRequest)
	request.SetReplyType(messagetypes.ActivityGetResultReply)

	return request
}

// GetActivityID gets the unique Id used to identify the activity.
//
// returns int64 -> the long ActivityID
func (request *ActivityGetResultRequest) GetActivityID() int64 {
	return request.GetLongProperty("ActivityId")
}

func (request *ActivityGetResultRequest) SetActivityID(value int64) {
	request.SetLongProperty("ActivityId", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ActivityRequest.Clone()
func (request *ActivityGetResultRequest) Clone() IProxyMessage {
	activityGetResultRequest := NewActivityGetResultRequest()
	var messageClone IProxyMessage = activityGetResultRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ActivityRequest.CopyTo()
func (request *ActivityGetResultRequest) CopyTo(target IProxyMessage) {
	request.ActivityRequest.CopyTo(target)
	if v, ok := target.(*ActivityGetResultRequest); ok {
		v.SetActivityID(request.GetActivityID())
	}
}
