//-----------------------------------------------------------------------------
// FILE:		workflow_describe_tasklist_reply.go
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

	// WorkflowDescribeTaskListReply is a WorkflowReply of MessageType
	// WorkflowDescribeTaskListReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowDescribeTaskListRequest
	WorkflowDescribeTaskListReply struct {
		*WorkflowReply
	}
)

// NewWorkflowDescribeTaskListReply is the default constructor for
// a WorkflowDescribeTaskListReply
//
// returns *WorkflowDescribeTaskListReply -> a pointer to a newly initialized
// WorkflowDescribeTaskListReply in memory
func NewWorkflowDescribeTaskListReply() *WorkflowDescribeTaskListReply {
	reply := new(WorkflowDescribeTaskListReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(internal.WorkflowDescribeTaskListReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowDescribeTaskListReply) Clone() IProxyMessage {
	workflowDescribeTaskListReply := NewWorkflowDescribeTaskListReply()
	var messageClone IProxyMessage = workflowDescribeTaskListReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowDescribeTaskListReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
}
