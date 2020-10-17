//-----------------------------------------------------------------------------
// FILE:		describe_taskqueue_reply.go
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

	"go.temporal.io/api/workflowservice/v1"
)

type (

	// DescribeTaskQueueReply is a ProxyReply of MessageType
	// DescribeTaskQueueReply.  It holds a reference to a ProxyReply in memory
	// and is the reply type to a DescribeTaskQueueRequest
	DescribeTaskQueueReply struct {
		*ProxyReply
	}
)

// NewDescribeTaskQueueReply is the default constructor for
// a DescribeTaskQueueReply
//
// returns *DescribeTaskQueueReply -> a pointer to a newly initialized
// DescribeTaskQueueReply in memory
func NewDescribeTaskQueueReply() *DescribeTaskQueueReply {
	reply := new(DescribeTaskQueueReply)
	reply.ProxyReply = NewProxyReply()
	reply.SetType(internal.DescribeTaskQueueReply)

	return reply
}

// GetResult gets the DescribeTaskQueueReply's Result property from its
// properties map, describes the task queue details.
//
// returns *workflowservice.DescribeTaskQueueResponse -> the response to the temporal
// describe task queue request.
func (reply *DescribeTaskQueueReply) GetResult() *workflowservice.DescribeTaskQueueResponse {
	resp := new(workflowservice.DescribeTaskQueueResponse)
	err := reply.GetJSONProperty("Result", resp)
	if err != nil {
		return nil
	}

	return resp
}

// SetResult sets the DescribeTaskQueueReply's Result property in its
// properties map, describes the task queue details.
//
// param value workflowservice*.DescribeTaskQueueResponse -> the response to the temporal
// describe task queue request.
func (reply *DescribeTaskQueueReply) SetResult(value *workflowservice.DescribeTaskQueueResponse) {
	reply.SetJSONProperty("Result", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Build inherits docs from ProxyReply.Build()
func (reply *DescribeTaskQueueReply) Build(e error, result ...interface{}) {
	reply.ProxyReply.Build(e)
	if len(result) > 0 {
		if v, ok := result[0].(*workflowservice.DescribeTaskQueueResponse); ok {
			reply.SetResult(v)
		}
	}
}

// Clone inherits docs from ProxyReply.Clone()
func (reply *DescribeTaskQueueReply) Clone() IProxyMessage {
	workflowDescribeTaskQueueReply := NewDescribeTaskQueueReply()
	var messageClone IProxyMessage = workflowDescribeTaskQueueReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyReply.CopyTo()
func (reply *DescribeTaskQueueReply) CopyTo(target IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
	if v, ok := target.(*DescribeTaskQueueReply); ok {
		v.SetResult(reply.GetResult())
	}
}
