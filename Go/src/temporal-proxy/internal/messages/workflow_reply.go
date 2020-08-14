//-----------------------------------------------------------------------------
// FILE:		workflow_reply.go
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
	proxytemporal "temporal-proxy/internal/temporal"
)

type (

	// WorkflowReply is base type for all workflow replies.
	// All workflow replies will inherit from WorkflowReply
	//
	// A WorkflowReply contains a reference to a
	// ProxyReply struct in memory
	WorkflowReply struct {
		*ProxyReply
	}

	// IWorkflowReply is the interface that all workflow message replies
	// implement.
	IWorkflowReply interface {
		IProxyReply
		GetContextID() int64
		SetContextID(value int64)
		GetReplayStatus() proxytemporal.ReplayStatus
		SetReplayStatus(status proxytemporal.ReplayStatus)
	}
)

// NewWorkflowReply is the default constructor for WorkflowReply.
// It creates a new WorkflowReply in memory and then creates and sets
// a reference to a new ProxyReply in the WorkflowReply.
//
// returns *WorkflowReply -> a pointer to a new WorkflowReply in memory
func NewWorkflowReply() *WorkflowReply {
	reply := new(WorkflowReply)
	reply.ProxyReply = NewProxyReply()
	reply.SetType(internal.Unspecified)

	return reply
}

// -------------------------------------------------------------------------
// IWorkflowReply interface methods for implementing the IWorkflowReply interface

// GetContextID gets the ContextId from a WorkflowReply's properties
// map.
//
// returns int64 -> the long representing a WorkflowReply's ContextId
func (reply *WorkflowReply) GetContextID() int64 {
	return reply.GetLongProperty("ContextId")
}

// SetContextID sets the ContextId in a WorkflowReply's properties map
//
// param value int64 -> int64 value to set as the WorkflowReply's ContextId
// in its properties map
func (reply *WorkflowReply) SetContextID(value int64) {
	reply.SetLongProperty("ContextId", value)
}

// GetReplayStatus gets the ReplayStatus from a WorkflowReply's properties
// map. For workflow requests related to an executing workflow,
// this will indicate the current history replay state.
//
// returns proxytemporal.ReplayStatus -> the current history replay
// state of a workflow
func (reply *WorkflowReply) GetReplayStatus() proxytemporal.ReplayStatus {
	replayStatusPtr := reply.GetStringProperty("ReplayStatus")
	if replayStatusPtr == nil {
		return proxytemporal.ReplayStatusUnspecified
	}
	replayStatus := proxytemporal.StringToReplayStatus(*replayStatusPtr)

	return replayStatus
}

// SetReplayStatus sets the ReplayStatus in a WorkflowReply's properties
// map. For workflow requests related to an executing workflow,
// this will indicate the current history replay state.
//
// param value proxytemporal.ReplayStatus -> the current history replay
// state of a workflow
func (reply *WorkflowReply) SetReplayStatus(value proxytemporal.ReplayStatus) {
	status := value.String()
	reply.SetStringProperty("ReplayStatus", &status)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Build inherits docs from ProxyReply.Build()
func (reply *WorkflowReply) Build(e error, result ...interface{}) {
	reply.ProxyReply.Build(e)
}

// Clone inherits docs from ProxyReply.Clone()
func (reply *WorkflowReply) Clone() IProxyMessage {
	workflowContextReply := NewWorkflowReply()
	var messageClone IProxyMessage = workflowContextReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyReply.CopyTo()
func (reply *WorkflowReply) CopyTo(target IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
	if v, ok := target.(IWorkflowReply); ok {
		v.SetContextID(reply.GetContextID())
		v.SetReplayStatus(reply.GetReplayStatus())
	}
}
