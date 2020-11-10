//-----------------------------------------------------------------------------
// FILE:		activity_reply.go
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

package handlers

import (
	"os"

	"go.temporal.io/sdk/activity"
	"go.uber.org/zap"

	"temporal-proxy/internal"
	"temporal-proxy/internal/messages"
)

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

	// get error values
	err := reply.GetError()

	// check if the activity is to be
	// completed externally
	var result interface{}
	if reply.GetPending() {
		result = activity.ErrResultPending
		err = nil
	} else {
		result = reply.GetResult()
	}

	// set the reply
	return op.SendChannel(result, err)
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
