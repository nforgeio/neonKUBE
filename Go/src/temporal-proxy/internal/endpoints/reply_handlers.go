//-----------------------------------------------------------------------------
// FILE:		reply_handlers.go
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

package endpoints

import (
	"errors"
	"os"
	"time"

	"go.temporal.io/temporal/activity"
	"go.temporal.io/temporal/workflow"
	"go.uber.org/zap"

	"temporal-proxy/internal"
	"temporal-proxy/internal/messages"
	proxyerror "temporal-proxy/internal/temporal/error"
)

// -------------------------------------------------------------------------
// client message types

func handleLogReply(reply *messages.LogReply, op *Operation) error {
	return op.SendChannel(true, reply.GetError())
}

// -------------------------------------------------------------------------
// Workflow message types

func handleWorkflowInvokeReply(reply *messages.WorkflowInvokeReply, op *Operation) error {
	defer WorkflowContexts.Remove(op.GetContextID())

	requestID := reply.GetRequestID()
	contextID := op.GetContextID()
	clientID := reply.GetClientID()
	Logger.Debug("Settling Workflow",
		zap.Int64("ClientId", clientID),
		zap.Int64("ContextId", contextID),
		zap.Int64("RequestId", requestID),
		zap.Int("ProcessId", os.Getpid()))

	// WorkflowContext at the specified WorflowContextID
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		return internal.ErrEntityNotExist
	}

	workflowName := *wectx.GetWorkflowName()
	Logger.Debug("WorkflowInfo", zap.String("Workflow", workflowName))

	// check for ForceReplay
	if reply.GetForceReplay() {
		return op.SendChannel(nil, proxyerror.NewTemporalError(errors.New("force-replay")))
	}

	// check for ContinueAsNew
	if reply.GetContinueAsNew() {
		continueContext := wectx.GetContext()
		if reply.GetContinueAsNewNamespace() != nil {
			continueContext = workflow.WithWorkflowNamespace(continueContext, *reply.GetContinueAsNewNamespace())
		}
		if reply.GetContinueAsNewTaskList() != nil {
			continueContext = workflow.WithTaskList(continueContext, *reply.GetContinueAsNewTaskList())
		}
		if reply.GetContinueAsNewExecutionStartToCloseTimeout() > 0 {
			continueContext = workflow.WithStartToCloseTimeout(continueContext, time.Duration(reply.GetContinueAsNewExecutionStartToCloseTimeout()))
		}
		if reply.GetContinueAsNewScheduleToCloseTimeout() > 0 {
			continueContext = workflow.WithScheduleToCloseTimeout(continueContext, time.Duration(reply.GetContinueAsNewScheduleToCloseTimeout()))
		}
		if reply.GetContinueAsNewScheduleToStartTimeout() > 0 {
			continueContext = workflow.WithScheduleToStartTimeout(continueContext, time.Duration(reply.GetContinueAsNewScheduleToStartTimeout()))
		}
		if reply.GetContinueAsNewStartToCloseTimeout() > 0 {
			continueContext = workflow.WithStartToCloseTimeout(continueContext, time.Duration(reply.GetContinueAsNewStartToCloseTimeout()))
		}
		continueAsNewWorkflow := workflowName
		if reply.GetContinueAsNewWorkflow() != nil {
			continueAsNewWorkflow = *reply.GetContinueAsNewWorkflow()
		}
		continueError := workflow.NewContinueAsNewError(continueContext, continueAsNewWorkflow, reply.GetContinueAsNewArgs())

		return op.SendChannel(continueError, nil)
	}

	// special errors
	var result interface{} = reply.GetResult()
	var temporalError *proxyerror.TemporalError = reply.GetError()
	if temporalError != nil {
		if isCanceledErr(temporalError) {
			result = workflow.ErrCanceled
			temporalError = nil
		}
	}

	// set the reply
	return op.SendChannel(result, temporalError)
}

func handleWorkflowSignalInvokeReply(reply *messages.WorkflowSignalInvokeReply, op *Operation) error {
	requestID := reply.GetRequestID()
	contextID := op.GetContextID()
	Logger.Debug("Settling Signal",
		zap.Int64("ClientId", reply.GetClientID()),
		zap.Int64("ContextId", contextID),
		zap.Int64("RequestId", requestID),
		zap.Int("ProcessId", os.Getpid()))

	// WorkflowContext at the specified WorflowContextID
	if wectx := WorkflowContexts.Get(contextID); wectx == nil {
		return internal.ErrEntityNotExist
	}

	// set the reply
	return op.SendChannel(true, reply.GetError())
}

func handleWorkflowQueryInvokeReply(reply *messages.WorkflowQueryInvokeReply, op *Operation) error {
	requestID := reply.GetRequestID()
	contextID := op.GetContextID()
	Logger.Debug("Settling Query",
		zap.Int64("ClientId", reply.GetClientID()),
		zap.Int64("ContextId", contextID),
		zap.Int64("RequestId", requestID),
		zap.Int("ProcessId", os.Getpid()))

	// WorkflowContext at the specified WorflowContextID
	if wectx := WorkflowContexts.Get(contextID); wectx == nil {
		return internal.ErrEntityNotExist
	}

	// set the reply
	return op.SendChannel(reply.GetResult(), reply.GetError())
}

func handleWorkflowFutureReadyReply(reply *messages.WorkflowFutureReadyReply, op *Operation) error {
	requestID := reply.GetRequestID()
	contextID := op.GetContextID()
	Logger.Debug("Settling Future ACK",
		zap.Int64("ClientId", reply.GetClientID()),
		zap.Int64("ContextId", contextID),
		zap.Int64("RequestId", requestID),
		zap.Int("ProcessId", os.Getpid()))

	// set the reply
	return op.SendChannel(true, nil)
}

// -------------------------------------------------------------------------
// Activity message types

func handleActivityInvokeReply(reply *messages.ActivityInvokeReply, op *Operation) error {
	defer ActivityContexts.Remove(op.GetContextID())

	requestID := reply.GetRequestID()
	contextID := op.GetContextID()
	Logger.Debug("Settling Activity",
		zap.Int64("ClientId", reply.GetClientID()),
		zap.Int64("ActivityContextId", contextID),
		zap.Int64("RequestId", requestID),
		zap.Int("ProcessId", os.Getpid()))

	// ActivityContext at the specified WorflowContextID
	if actx := ActivityContexts.Get(contextID); actx == nil {
		return internal.ErrEntityNotExist
	}

	// check if the activity is to be
	// completed externally
	var result interface{}
	if reply.GetPending() {
		result = activity.ErrResultPending
	} else {
		result = reply.GetResult()
	}

	// set the reply
	return op.SendChannel(result, reply.GetError())
}

func handleActivityStoppingReply(reply *messages.ActivityStoppingReply, op *Operation) error {
	requestID := reply.GetRequestID()
	contextID := op.GetContextID()
	Logger.Debug("Settling Activity Stopping",
		zap.Int64("ClientId", reply.GetClientID()),
		zap.Int64("ActivityContextId", contextID),
		zap.Int64("RequestId", requestID),
		zap.Int("ProcessId", os.Getpid()))

	// ActivityContext at the specified WorflowContextID
	if actx := ActivityContexts.Get(contextID); actx == nil {
		return internal.ErrEntityNotExist
	}

	// set the reply
	return op.SendChannel(true, reply.GetError())
}

func handleActivityInvokeLocalReply(reply *messages.ActivityInvokeLocalReply, op *Operation) error {
	defer ActivityContexts.Remove(op.GetContextID())

	requestID := reply.GetRequestID()
	contextID := op.GetContextID()
	Logger.Debug("Settling Local Activity",
		zap.Int64("ClientId", reply.GetClientID()),
		zap.Int64("ActivityContextId", contextID),
		zap.Int64("RequestId", requestID),
		zap.Int("ProcessId", os.Getpid()))

	// ActivityContext at the specified WorflowContextID
	if actx := ActivityContexts.Get(contextID); actx == nil {
		return internal.ErrEntityNotExist
	}

	// set the reply
	return op.SendChannel(reply.GetResult(), reply.GetError())
}
