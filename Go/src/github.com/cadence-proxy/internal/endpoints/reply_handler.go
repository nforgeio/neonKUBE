//-----------------------------------------------------------------------------
// FILE:		reply_handler.go
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
	"fmt"
	"os"
	"time"

	"go.uber.org/cadence/activity"
	"go.uber.org/cadence/workflow"
	"go.uber.org/zap"

	globals "github.com/cadence-proxy/internal"
	"github.com/cadence-proxy/internal/cadence/cadenceerrors"
	"github.com/cadence-proxy/internal/messages"
	messagetypes "github.com/cadence-proxy/internal/messages/types"
)

// -------------------------------------------------------------------------
// IProxyReply message type handlers

func handleIProxyReply(reply messages.IProxyReply) error {

	// error to catch any exceptions thrown in the
	// switch block
	var err error

	// handle the messages individually based on their message type
	switch reply.GetType() {

	// -------------------------------------------------------------------------
	// client message types

	// TerminateReply
	case messagetypes.TerminateReply:
		if v, ok := reply.(*messages.TerminateReply); ok {
			err = handleTerminateReply(v)
		}

	// -------------------------------------------------------------------------
	// Workflow message types

	// WorkflowInvokeReply
	case messagetypes.WorkflowInvokeReply:
		if v, ok := reply.(*messages.WorkflowInvokeReply); ok {
			err = handleWorkflowInvokeReply(v)
		}

	// WorkflowSignalInvokeReply
	case messagetypes.WorkflowSignalInvokeReply:
		if v, ok := reply.(*messages.WorkflowSignalInvokeReply); ok {
			err = handleWorkflowSignalInvokeReply(v)
		}

	// WorkflowQueryInvokeReply
	case messagetypes.WorkflowQueryInvokeReply:
		if v, ok := reply.(*messages.WorkflowQueryInvokeReply); ok {
			err = handleWorkflowQueryInvokeReply(v)
		}

	// -------------------------------------------------------------------------
	// Activity message types

	// ActivityInvokeReply
	case messagetypes.ActivityInvokeReply:
		if v, ok := reply.(*messages.ActivityInvokeReply); ok {
			err = handleActivityInvokeReply(v)
		}

	// ActivityStoppingReply
	case messagetypes.ActivityStoppingReply:
		if v, ok := reply.(*messages.ActivityStoppingReply); ok {
			err = handleActivityStoppingReply(v)
		}

	// ActivityInvokeLocalReply
	case messagetypes.ActivityInvokeLocalReply:
		if v, ok := reply.(*messages.ActivityInvokeLocalReply); ok {
			err = handleActivityInvokeLocalReply(v)
		}

	// Undefined message type
	default:

		err = fmt.Errorf("unhandled message type. could not complete type assertion for type %d", reply.GetType())

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Unhandled message type. Could not complete type assertion", zap.Error(err))
	}

	// catch any exceptions returned in
	// the switch block
	if err != nil {
		return err
	}

	return nil
}

// -------------------------------------------------------------------------
// ProxyReply handlers

// -------------------------------------------------------------------------
// Client message types

func handleTerminateReply(reply *messages.TerminateReply) error {
	err := fmt.Errorf("not implemented exception for message type TerminateReply")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Error handling TerminateReply", zap.Error(err))
	return err
}

// -------------------------------------------------------------------------
// Workflow message types

func handleWorkflowInvokeReply(reply *messages.WorkflowInvokeReply) error {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowInvokeReply Received", zap.Int("ProccessId", os.Getpid()))

	// remove the WorkflowContext from the map
	// and remove the Operation from the map
	requestID := reply.GetRequestID()
	defer func() {
		_ = WorkflowContexts.Remove(Operations.Get(requestID).GetContextID())
		_ = Operations.Remove(requestID)
	}()

	// get the Operation corresponding the the reply
	op := Operations.Get(requestID)
	if op == nil {
		return globals.ErrEntityNotExist
	}
	contextID := op.GetContextID()

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Settling Workflow",
		zap.Int64("ContextId", contextID),
		zap.Int64("RequestId", requestID),
		zap.Int("ProccessId", os.Getpid()),
	)

	// WorkflowContext at the specified WorflowContextID
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		return globals.ErrEntityNotExist
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
	var cadenceError *cadenceerrors.CadenceError = reply.GetError()
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

func handleWorkflowSignalInvokeReply(reply *messages.WorkflowSignalInvokeReply) error {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowSignalInvokeReply Received", zap.Int("ProccessId", os.Getpid()))

	// remove the WorkflowContext from the map
	// and remove the Operation from the map
	requestID := reply.GetRequestID()
	defer Operations.Remove(requestID)

	// get the Operation corresponding the the reply
	op := Operations.Get(requestID)
	if op == nil {
		return globals.ErrEntityNotExist
	}

	// $debug(jack.burns): DELETE THIS!
	contextID := op.GetContextID()
	logger.Debug("Settling Signal",
		zap.Int64("ContextId", contextID),
		zap.Int64("RequestId", requestID),
		zap.Int("ProccessId", os.Getpid()),
	)

	// WorkflowContext at the specified WorflowContextID
	if wectx := WorkflowContexts.Get(contextID); wectx == nil {
		return globals.ErrEntityNotExist
	}

	// set the reply
	err := op.SendChannel(true, reply.GetError())
	if err != nil {
		return err
	}

	return nil
}

func handleWorkflowQueryInvokeReply(reply *messages.WorkflowQueryInvokeReply) error {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowQueryInvokeReply Received", zap.Int("ProccessId", os.Getpid()))

	// remove the WorkflowContext from the map
	// and remove the Operation from the map
	requestID := reply.GetRequestID()
	defer Operations.Remove(requestID)

	// get the Operation corresponding the the reply
	op := Operations.Get(requestID)
	if op == nil {
		return globals.ErrEntityNotExist
	}

	// $debug(jack.burns): DELETE THIS!
	contextID := op.GetContextID()
	logger.Debug("Settling Query",
		zap.Int64("ContextId", contextID),
		zap.Int64("RequestId", requestID),
		zap.Int("ProccessId", os.Getpid()),
	)

	// WorkflowContext at the specified WorflowContextID
	if wectx := WorkflowContexts.Get(contextID); wectx == nil {
		return globals.ErrEntityNotExist
	}

	// set the reply
	err := op.SendChannel(reply.GetResult(), reply.GetError())
	if err != nil {
		return err
	}

	return nil
}

// -------------------------------------------------------------------------
// Activity message types

func handleActivityInvokeReply(reply *messages.ActivityInvokeReply) error {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("ActivityInvokeReply Received", zap.Int("ProccessId", os.Getpid()))

	// remove the WorkflowContext from the map
	// and remove the Operation from the map
	requestID := reply.GetRequestID()
	defer func() {
		_ = ActivityContexts.Remove(Operations.Get(requestID).GetContextID())
		_ = Operations.Remove(requestID)
	}()

	// get the Operation corresponding the the reply
	op := Operations.Get(requestID)
	if op == nil {
		return globals.ErrEntityNotExist
	}
	contextID := op.GetContextID()

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Settling Activity",
		zap.Int64("ActivityContextId", contextID),
		zap.Int64("RequestId", requestID),
		zap.Int("ProccessId", os.Getpid()),
	)

	// ActivityContext at the specified WorflowContextID
	if actx := ActivityContexts.Get(contextID); actx == nil {
		return globals.ErrEntityNotExist
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

func handleActivityStoppingReply(reply *messages.ActivityStoppingReply) error {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("ActivityStoppingReply Received", zap.Int("ProccessId", os.Getpid()))

	// remove the Operation from the map
	requestID := reply.GetRequestID()
	defer Operations.Remove(requestID)

	// get the Operation corresponding the the reply
	op := Operations.Get(requestID)
	if op == nil {
		return globals.ErrEntityNotExist
	}

	// set the reply
	err := op.SendChannel(true, reply.GetError())
	if err != nil {
		return err
	}

	return nil
}

func handleActivityInvokeLocalReply(reply *messages.ActivityInvokeLocalReply) error {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("ActivityInvokeLocalReply Received", zap.Int("ProccessId", os.Getpid()))

	// remove the WorkflowContext from the map
	// and remove the Operation from the map
	requestID := reply.GetRequestID()
	defer func() {
		_ = ActivityContexts.Remove(Operations.Get(requestID).GetContextID())
		_ = Operations.Remove(requestID)
	}()

	// get the Operation corresponding the the reply
	op := Operations.Get(requestID)
	if op == nil {
		return globals.ErrEntityNotExist
	}

	// ActivityContext at the specified WorflowContextID
	if actx := ActivityContexts.Get(op.GetContextID()); actx == nil {
		return globals.ErrEntityNotExist
	}

	// set the reply
	err := op.SendChannel(reply.GetResult(), reply.GetError())
	if err != nil {
		return err
	}

	return nil
}
