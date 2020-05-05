//-----------------------------------------------------------------------------
// FILE:		workflow_invoke_reply.go
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

	// WorkflowInvokeReply is a WorkflowReply of MessageType
	// WorkflowInvokeReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowInvokeRequest
	WorkflowInvokeReply struct {
		*WorkflowReply
	}
)

// NewWorkflowInvokeReply is the default constructor for
// a WorkflowInvokeReply
//
// returns *WorkflowInvokeReply -> a pointer to a newly initialized
// WorkflowInvokeReply in memory
func NewWorkflowInvokeReply() *WorkflowInvokeReply {
	reply := new(WorkflowInvokeReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(internal.WorkflowInvokeReply)

	return reply
}

// GetResult gets the workflow execution result or nil
// from a WorkflowInvokeReply's properties map.
//
// returns []byte -> []byte representing the result of a workflow execution
func (reply *WorkflowInvokeReply) GetResult() []byte {
	return reply.GetBytesProperty("Result")
}

// SetResult sets the workflow execution result or nil
// in a WorkflowInvokeReply's properties map.
//
// param value []byte -> []byte representing the result of a workflow execution
// to be set in the WorkflowInvokeReply's properties map
func (reply *WorkflowInvokeReply) SetResult(value []byte) {
	reply.SetBytesProperty("Result", value)
}

// GetContinueAsNew gets the WorkflowInvokeReply's ContinueAsNew bool property
// from its properties map.  Indicates whether the workflow should be exited and then restarted,
// with an empty history.  This is useful for very long running looping
// workflows that would otherwise end up with very long task histories.
//
// returns bool -> The bool value of the ContinueAsNew property in the WorkflowInvokeReply's
// properties map
func (reply *WorkflowInvokeReply) GetContinueAsNew() bool {
	return reply.GetBoolProperty("ContinueAsNew")
}

// SetContinueAsNew sets the WorkflowInvokeReply's ContinueAsNew bool property
// in its properties map.  Indicates whether the workflow should be exited and then restarted,
// with an empty history.  This is useful for very long running looping
// workflows that would otherwise end up with very long task histories.
//
// param value bool -> The bool value to set as the ContinueAsNew property in the WorkflowInvokeReply's
// properties map
func (reply *WorkflowInvokeReply) SetContinueAsNew(value bool) {
	reply.SetBoolProperty("ContinueAsNew", value)
}

// GetContinueAsNewArgs gets ContinueAsNew arguments or nil
// from a WorkflowInvokeReply's properties map. Specifies the arguments to use
// for the new workflow when ContinueAsNew is true.
//
// returns []byte -> []byte representing the result of a workflow execution
func (reply *WorkflowInvokeReply) GetContinueAsNewArgs() []byte {
	return reply.GetBytesProperty("ContinueAsNewArgs")
}

// SetContinueAsNewArgs sets the ContinueAsNew arguments or nil
// in a WorkflowInvokeReply's properties map. Specifies the arguments to use
// for the new workflow when ContinueAsNew is true.
//
// param value []byte -> []byte representing the result of a workflow execution
// to be set in the WorkflowInvokeReply's properties map
func (reply *WorkflowInvokeReply) SetContinueAsNewArgs(value []byte) {
	reply.SetBytesProperty("ContinueAsNewArgs", value)
}

// GetContinueAsNewExecutionStartToCloseTimeout gets ContinueAsNewExecutionStartToCloseTimeout
// arguments from a WorkflowInvokeReply's properties map. Optionally overrides the current workflow's
// timeout for the restarted workflow when this value is greater than zero.
// is true.
//
// returns int64 -> int64 representing the ContinueAsNewExecutionStartToCloseTimeout
func (reply *WorkflowInvokeReply) GetContinueAsNewExecutionStartToCloseTimeout() int64 {
	return reply.GetLongProperty("ContinueAsNewExecutionStartToCloseTimeout")
}

// SetContinueAsNewExecutionStartToCloseTimeout sets the ContinueAsNewExecutionStartToCloseTimeout
// in a WorkflowInvokeReply's properties map. Optionally overrides the current workflow's
// timeout for the restarted workflow when this value is greater than zero.
// is true.
//
// param value int64 -> int64 representing the ContinueAsNewExecutionStartToCloseTimeout
// to be set in the WorkflowInvokeReply's properties map
func (reply *WorkflowInvokeReply) SetContinueAsNewExecutionStartToCloseTimeout(value int64) {
	reply.SetLongProperty("ContinueAsNewExecutionStartToCloseTimeout", value)
}

// GetContinueAsNewScheduleToCloseTimeout gets ContinueAsNewScheduleToCloseTimeout
// arguments from a WorkflowInvokeReply's properties map. Optionally overrides the current workflow's
// timeout for the restarted workflow when this value is greater than zero.
// is true.
//
// returns int64 -> int64 representing the ContinueAsNewScheduleToCloseTimeout
func (reply *WorkflowInvokeReply) GetContinueAsNewScheduleToCloseTimeout() int64 {
	return reply.GetLongProperty("ContinueAsNewScheduleToCloseTimeout")
}

// SetContinueAsNewScheduleToCloseTimeout sets the ContinueAsNewScheduleToCloseTimeout
// in a WorkflowInvokeReply's properties map. Optionally overrides the current workflow's
// timeout for the restarted workflow when this value is greater than zero.
// is true.
//
// param value int64 -> int64 representing the ContinueAsNewScheduleToCloseTimeout
// to be set in the WorkflowInvokeReply's properties map
func (reply *WorkflowInvokeReply) SetContinueAsNewScheduleToCloseTimeout(value int64) {
	reply.SetLongProperty("ContinueAsNewScheduleToCloseTimeout", value)
}

// GetContinueAsNewScheduleToStartTimeout gets ContinueAsNewScheduleToStartTimeout
// arguments from a WorkflowInvokeReply's properties map. Optionally overrides the current workflow's
// timeout for the restarted workflow when this value is greater than zero.
// is true.
//
// returns int64 -> int64 representing the ContinueAsNewScheduleToStartTimeout
func (reply *WorkflowInvokeReply) GetContinueAsNewScheduleToStartTimeout() int64 {
	return reply.GetLongProperty("ContinueAsNewScheduleToStartTimeout")
}

// SetContinueAsNewScheduleToStartTimeout sets the ContinueAsNewScheduleToStartTimeout
// in a WorkflowInvokeReply's properties map. Optionally overrides the current workflow's
// timeout for the restarted workflow when this value is greater than zero.
// is true.
//
// param value int64 -> int64 representing the ContinueAsNewScheduleToStartTimeout
// to be set in the WorkflowInvokeReply's properties map
func (reply *WorkflowInvokeReply) SetContinueAsNewScheduleToStartTimeout(value int64) {
	reply.SetLongProperty("ContinueAsNewScheduleToStartTimeout", value)
}

// GetContinueAsNewStartToCloseTimeout gets ContinueAsNewStartToCloseTimeout
// arguments from a WorkflowInvokeReply's properties map. Optionally overrides the current workflow's
// timeout for the restarted workflow when this value is greater than zero.
// is true.
//
// returns int64 -> int64 representing the ContinueAsNewStartToCloseTimeout
func (reply *WorkflowInvokeReply) GetContinueAsNewStartToCloseTimeout() int64 {
	return reply.GetLongProperty("ContinueAsNewStartToCloseTimeout")
}

// SetContinueAsNewStartToCloseTimeout sets the ContinueAsNewStartToCloseTimeout
// in a WorkflowInvokeReply's properties map. Optionally overrides the current workflow's
// timeout for the restarted workflow when this value is greater than zero.
// is true.
//
// param value int64 -> int64 representing the ContinueAsNewStartToCloseTimeout
// to be set in the WorkflowInvokeReply's properties map
func (reply *WorkflowInvokeReply) SetContinueAsNewStartToCloseTimeout(value int64) {
	reply.SetLongProperty("ContinueAsNewStartToCloseTimeout", value)
}

// GetContinueAsNewWorkflow gets ContinueAsNewWorkflow
// arguments from a WorkflowInvokeReply's properties map. Optionally overrides the current
// workflow scheduled for execution upon restart when this value is not nil.
//
// returns *string -> pointer to a string in memory representing the ContinueAsNewWorkflow
func (reply *WorkflowInvokeReply) GetContinueAsNewWorkflow() *string {
	return reply.GetStringProperty("ContinueAsNewWorkflow")
}

// SetContinueAsNewWorkflow sets the ContinueAsNewWorkflow
// in a WorkflowInvokeReply's properties map. Optionally overrides the current
// workflow scheduled for execution upon restart when this value is not nil.
//
// param value *string -> pointer to a string in memory representing the
// ContinueAsNewWorkflow to be set in the WorkflowInvokeReply's properties map
func (reply *WorkflowInvokeReply) SetContinueAsNewWorkflow(value *string) {
	reply.SetStringProperty("ContinueAsNewWorkflow", value)
}

// GetContinueAsNewTaskList gets ContinueAsNewTaskList
// arguments from a WorkflowInvokeReply's properties map. Optionally overrides the current
// workflow's tasklist for the restarted workflow when this value is not nil
//
// returns *string -> pointer to a string in memory representing the ContinueAsNewTaskList
func (reply *WorkflowInvokeReply) GetContinueAsNewTaskList() *string {
	return reply.GetStringProperty("ContinueAsNewTaskList")
}

// SetContinueAsNewTaskList sets the ContinueAsNewTaskList
// in a WorkflowInvokeReply's properties map.Optionally overrides the current
// workflow's tasklist for the restarted workflow when this value is not nil
//
// param value *string -> pointer to a string in memory representing the
// ContinueAsNewTaskList to be set in the WorkflowInvokeReply's properties map
func (reply *WorkflowInvokeReply) SetContinueAsNewTaskList(value *string) {
	reply.SetStringProperty("ContinueAsNewTaskList", value)
}

// GetContinueAsNewDomain gets ContinueAsNewDomain
// arguments from a WorkflowInvokeReply's properties map. Optionally overrides the current
// workflow's domain for the restarted workflow when this value is not nil
//
// returns *string -> pointer to a string in memory representing the ContinueAsNewDomain
func (reply *WorkflowInvokeReply) GetContinueAsNewDomain() *string {
	return reply.GetStringProperty("ContinueAsNewDomain")
}

// SetContinueAsNewDomain sets the ContinueAsNewDomain
// in a WorkflowInvokeReply's properties map.Optionally overrides the current
// workflow's domain for the restarted workflow when this value is not nil
//
// param value *string -> pointer to a string in memory representing the
// ContinueAsNewDomain to be set in the WorkflowInvokeReply's properties map
func (reply *WorkflowInvokeReply) SetContinueAsNewDomain(value *string) {
	reply.SetStringProperty("ContinueAsNewDomain", value)
}

// GetForceReplay returns the property indicate that the current workflow
// execution should be aboorted such that it will be rescheduled and replayed
// as required.
func (reply *WorkflowInvokeReply) GetForceReplay() bool {
	return reply.GetBoolProperty("ForceReplay")
}

// SetForceReplay sets the property indicate that the current workflow
// execution should be aboorted such that it will be rescheduled and replayed
// as required.
func (reply *WorkflowInvokeReply) SetForceReplay(value bool) {
	reply.SetBoolProperty("ForceReplay", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (reply *WorkflowInvokeReply) Clone() IProxyMessage {
	workflowInvokeReply := NewWorkflowInvokeReply()
	var messageClone IProxyMessage = workflowInvokeReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *WorkflowInvokeReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
	if v, ok := target.(*WorkflowInvokeReply); ok {
		v.SetResult(reply.GetResult())
		v.SetContinueAsNew(reply.GetContinueAsNew())
		v.SetContinueAsNewArgs(reply.GetContinueAsNewArgs())
		v.SetContinueAsNewExecutionStartToCloseTimeout(reply.GetContinueAsNewExecutionStartToCloseTimeout())
		v.SetContinueAsNewScheduleToCloseTimeout(reply.GetContinueAsNewScheduleToCloseTimeout())
		v.SetContinueAsNewScheduleToStartTimeout(reply.GetContinueAsNewScheduleToStartTimeout())
		v.SetContinueAsNewStartToCloseTimeout(reply.GetContinueAsNewStartToCloseTimeout())
		v.SetContinueAsNewWorkflow(reply.GetContinueAsNewWorkflow())
		v.SetContinueAsNewTaskList(reply.GetContinueAsNewTaskList())
		v.SetContinueAsNewDomain(reply.GetContinueAsNewDomain())
		v.SetForceReplay(reply.GetForceReplay())
	}
}
