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

	"go.uber.org/cadence/activity"
	"go.uber.org/cadence/workflow"
	"go.uber.org/zap"

	"github.com/cadence-proxy/internal"
	proxyerror "github.com/cadence-proxy/internal/cadence/error"
	"github.com/cadence-proxy/internal/messages"
)

// -------------------------------------------------------------------------
// client message types

func handleLogReply(reply *messages.LogReply, op *messages.Operation) error {
	err := op.SendChannel(true, reply.GetError())
	if err != nil {
		return err
	}

	return nil
}

// -------------------------------------------------------------------------
// Workflow message types

func handleWorkflowInvokeReply(reply *messages.WorkflowInvokeReply, op *messages.Operation) error {
	defer func() {
		_ = WorkflowContexts.Remove(op.GetContextID())
	}()

	requestID := reply.GetRequestID()
	contextID := op.GetContextID()
	internal.Logger.Debug("Settling Workflow",
		zap.Int64("ContextId", contextID),
		zap.Int64("RequestId", requestID),
		zap.Int("ProcessId", os.Getpid()),
	)

	// WorkflowContext at the specified WorflowContextID
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		return internal.ErrEntityNotExist
	}

	// check for ForceReplay
	if reply.GetForceReplay() {
		err := op.SendChannel(nil, proxyerror.NewCadenceError(errors.New("force-replay")))
		if err != nil {
			return err
		}

		return nil
	}

	// check for ContinueAsNew
	if reply.GetContinueAsNew() {
		continueContext := wectx.GetContext()

		if reply.GetContinueAsNewDomain() != nil {
			continueContext = workflow.WithWorkflowDomain(continueContext, *reply.GetContinueAsNewDomain())
		}

		if reply.GetContinueAsNewTaskList() != nil {
			continueContext = workflow.WithTaskList(continueContext, *reply.GetContinueAsNewTaskList())
		}

		if reply.GetContinueAsNewExecutionStartToCloseTimeout() > 0 {
			continueContext = workflow.WithExecutionStartToCloseTimeout(continueContext, time.Duration(reply.GetContinueAsNewExecutionStartToCloseTimeout()))
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

		// Start a continue as new instance of the workflow and get the error to send
		// back to the Neon.Cadence Lib
		// set ContinueAsNewError as the result
		continueError := workflow.NewContinueAsNewError(continueContext, *wectx.GetWorkflowName(), reply.GetContinueAsNewArgs())
		err := op.SendChannel(continueError, nil)
		if err != nil {
			return err
		}

		return nil
	}

	// special errors
	var result interface{} = reply.GetResult()
	var cadenceError *proxyerror.CadenceError = reply.GetError()
	if cadenceError != nil {
		if isCanceledErr(cadenceError) {
			result = workflow.ErrCanceled
			cadenceError = nil
		}
	}

	// set the reply
	err := op.SendChannel(result, cadenceError)
	if err != nil {
		return err
	}

	return nil
}

func handleWorkflowSignalInvokeReply(reply *messages.WorkflowSignalInvokeReply, op *messages.Operation) error {
	requestID := reply.GetRequestID()
	contextID := op.GetContextID()
	internal.Logger.Debug("Settling Signal",
		zap.Int64("ContextId", contextID),
		zap.Int64("RequestId", requestID),
		zap.Int("ProcessId", os.Getpid()),
	)

	// WorkflowContext at the specified WorflowContextID
	if wectx := WorkflowContexts.Get(contextID); wectx == nil {
		return internal.ErrEntityNotExist
	}

	// set the reply
	err := op.SendChannel(true, reply.GetError())
	if err != nil {
		return err
	}

	return nil
}

func handleWorkflowQueryInvokeReply(reply *messages.WorkflowQueryInvokeReply, op *messages.Operation) error {
	requestID := reply.GetRequestID()
	contextID := op.GetContextID()
	internal.Logger.Debug("Settling Query",
		zap.Int64("ContextId", contextID),
		zap.Int64("RequestId", requestID),
		zap.Int("ProcessId", os.Getpid()),
	)

	// WorkflowContext at the specified WorflowContextID
	if wectx := WorkflowContexts.Get(contextID); wectx == nil {
		return internal.ErrEntityNotExist
	}

	// set the reply
	err := op.SendChannel(reply.GetResult(), reply.GetError())
	if err != nil {
		return err
	}

	return nil
}

func handleWorkflowFutureReadyReply(reply *messages.WorkflowFutureReadyReply, op *messages.Operation) error {
	requestID := reply.GetRequestID()
	contextID := op.GetContextID()
	internal.Logger.Debug("Settling Future ACK",
		zap.Int64("ContextId", contextID),
		zap.Int64("RequestId", requestID),
		zap.Int("ProcessId", os.Getpid()),
	)

	// set the reply
	err := op.SendChannel(true, nil)
	if err != nil {
		return err
	}

	return nil
}

// -------------------------------------------------------------------------
// Activity message types

func handleActivityInvokeReply(reply *messages.ActivityInvokeReply, op *messages.Operation) error {
	defer func() {
		_ = ActivityContexts.Remove(op.GetContextID())
	}()

	requestID := reply.GetRequestID()
	contextID := op.GetContextID()
	internal.Logger.Debug("Settling Activity",
		zap.Int64("ActivityContextId", contextID),
		zap.Int64("RequestId", requestID),
		zap.Int("ProcessId", os.Getpid()),
	)

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
	err := op.SendChannel(result, reply.GetError())
	if err != nil {
		return err
	}

	return nil
}

func handleActivityStoppingReply(reply *messages.ActivityStoppingReply, op *messages.Operation) error {
	requestID := reply.GetRequestID()
	contextID := op.GetContextID()
	internal.Logger.Debug("Settling Activity Stopping",
		zap.Int64("ActivityContextId", contextID),
		zap.Int64("RequestId", requestID),
		zap.Int("ProcessId", os.Getpid()),
	)

	// set the reply
	err := op.SendChannel(true, reply.GetError())
	if err != nil {
		return err
	}

	return nil
}

func handleActivityInvokeLocalReply(reply *messages.ActivityInvokeLocalReply, op *messages.Operation) error {
	defer func() {
		_ = ActivityContexts.Remove(op.GetContextID())
	}()

	requestID := reply.GetRequestID()
	contextID := op.GetContextID()
	internal.Logger.Debug("Settling Local Activity",
		zap.Int64("ActivityContextId", contextID),
		zap.Int64("RequestId", requestID),
		zap.Int("ProcessId", os.Getpid()),
	)

	// ActivityContext at the specified WorflowContextID
	if actx := ActivityContexts.Get(contextID); actx == nil {
		return internal.ErrEntityNotExist
	}

	// set the reply
	err := op.SendChannel(reply.GetResult(), reply.GetError())
	if err != nil {
		return err
	}

	return nil
}
