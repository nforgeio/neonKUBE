//-----------------------------------------------------------------------------
// FILE:		describe_tasklist_reply.go
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

	"go.temporal.io/temporal-proto/workflowservice"
)

type (

	// DescribeTaskListReply is a ProxyReply of MessageType
	// DescribeTaskListReply.  It holds a reference to a ProxyReply in memory
	// and is the reply type to a DescribeTaskListRequest
	DescribeTaskListReply struct {
		*ProxyReply
	}
)

// NewDescribeTaskListReply is the default constructor for
// a DescribeTaskListReply
//
// returns *DescribeTaskListReply -> a pointer to a newly initialized
// DescribeTaskListReply in memory
func NewDescribeTaskListReply() *DescribeTaskListReply {
	reply := new(DescribeTaskListReply)
	reply.ProxyReply = NewProxyReply()
	reply.SetType(internal.DescribeTaskListReply)

	return reply
}

// GetResult gets the DescribeTaskListReply's Result property from its
// properties map, describes the task list details.
//
// returns *workflowservice.DescribeTaskListResponse -> the response to the temporal
// describe task list request.
func (reply *DescribeTaskListReply) GetResult() *workflowservice.DescribeTaskListResponse {
	resp := new(workflowservice.DescribeTaskListResponse)
	err := reply.GetJSONProperty("Result", resp)
	if err != nil {
		return nil
	}

	return resp
}

// SetResult sets the DescribeTaskListReply's Result property in its
// properties map, describes the task list details.
//
// param value workflowservice*.DescribeTaskListResponse -> the response to the temporal
// describe task list request.
func (reply *DescribeTaskListReply) SetResult(value *workflowservice.DescribeTaskListResponse) {
	reply.SetJSONProperty("Result", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Build inherits docs from ProxyReply.Build()
func (reply *DescribeTaskListReply) Build(e error, result ...interface{}) {
	reply.ProxyReply.Build(e)
	if len(result) > 0 {
		if v, ok := result[0].(*workflowservice.DescribeTaskListResponse); ok {
			reply.SetResult(v)
		}
	}
}

// Clone inherits docs from ProxyReply.Clone()
func (reply *DescribeTaskListReply) Clone() IProxyMessage {
	workflowDescribeTaskListReply := NewDescribeTaskListReply()
	var messageClone IProxyMessage = workflowDescribeTaskListReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyReply.CopyTo()
func (reply *DescribeTaskListReply) CopyTo(target IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
	if v, ok := target.(*DescribeTaskListReply); ok {
		v.SetResult(reply.GetResult())
	}
}
