// -----------------------------------------------------------------------------
// FILE:		activity_request.go
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
	"context"
	"fmt"
	"os"
	"reflect"

	"go.temporal.io/sdk/activity"
	"go.temporal.io/sdk/workflow"
	"go.uber.org/zap"

	"temporal-proxy/internal"
	"temporal-proxy/internal/messages"
	proxyactivity "temporal-proxy/internal/temporal/activity"
	proxyworkflow "temporal-proxy/internal/temporal/workflow"
)

// ----------------------------------------------------------------------
// IProxyRequest activity message type handler methods

func handleActivityRegisterRequest(requestCtx context.Context, request *messages.ActivityRegisterRequest) messages.IProxyReply {
	activityName := *request.GetName()
	clientID := request.GetClientID()
	workerID := request.GetWorkerID()
	Logger.Debug("ActivityRegisterRequest Received",
		zap.String("Activity", activityName),
		zap.Int64("WorkerId", workerID),
		zap.Int64("ClientId", clientID),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()))

	// new ActivityRegisterReply
	reply := messages.CreateReplyMessage(request)

	clientHelper := Clients.Get(clientID)
	if clientHelper == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	// define the activity function
	activityFunc := func(ctx context.Context, input []byte) ([]byte, error) {
		requestID := NextRequestID()
		contextID := proxyactivity.NextContextID()
		Logger.Debug("Executing Activity",
			zap.String("Activity", activityName),
			zap.Int64("ClientId", clientID),
			zap.Int64("ActivityContextId", contextID),
			zap.Int64("RequestId", requestID),
			zap.Int("ProcessId", os.Getpid()))

		// add the context to ActivityContexts
		actx := proxyactivity.NewActivityContext(ctx)
		actx.SetActivityName(&activityName)
		contextID = ActivityContexts.Add(contextID, actx)

		// Send a ActivityInvokeRequest to the Neon.Temporal Lib
		// temporal-client
		activityInvokeRequest := messages.NewActivityInvokeRequest()
		activityInvokeRequest.SetRequestID(requestID)
		activityInvokeRequest.SetArgs(input)
		activityInvokeRequest.SetContextID(contextID)
		activityInvokeRequest.SetActivity(&activityName)
		activityInvokeRequest.SetClientID(clientID)

		// create the Operation for this request and add it to the operations map
		op := NewOperation(requestID, activityInvokeRequest)
		op.SetChannel(make(chan interface{}))
		op.SetContextID(contextID)
		Operations.Add(requestID, op)

		// get worker stop channel on the context
		// Send and wait for
		// ActivityStoppingRequest
		stopChan := activity.GetWorkerStopChannel(ctx)
		s := func() {
			<-stopChan
			requestID := NextRequestID()
			Logger.Debug("Stopping Activity",
				zap.String("Activity", activityName),
				zap.Int64("ClientId", clientID),
				zap.Int64("ActivityContextId", contextID),
				zap.Int64("RequestId", requestID),
				zap.Int("ProcessId", os.Getpid()))

			// send an ActivityStoppingRequest to the client
			activityStoppingRequest := messages.NewActivityStoppingRequest()
			activityStoppingRequest.SetRequestID(requestID)
			activityStoppingRequest.SetActivityID(&activityName)
			activityStoppingRequest.SetContextID(contextID)
			activityStoppingRequest.SetClientID(clientID)

			// create the Operation for this request and add it to the operations map
			stoppingReplyChan := make(chan interface{})
			op := NewOperation(requestID, activityStoppingRequest)
			op.SetChannel(stoppingReplyChan)
			op.SetContextID(contextID)
			Operations.Add(requestID, op)

			// send the request and wait for the reply
			go sendMessage(activityStoppingRequest)

			Logger.Debug("ActivityStoppingRequest sent",
				zap.String("Activity", activityName),
				zap.Int64("ClientId", clientID),
				zap.Int64("ActivityContextId", contextID),
				zap.Int64("RequestId", requestID),
				zap.Int("ProcessId", os.Getpid()))

			<-stoppingReplyChan
		}

		// run go routines
		go s()
		go sendMessage(activityInvokeRequest)

		Logger.Debug("ActivityInvokeRequest sent",
			zap.String("Activity", activityName),
			zap.Int64("ClientId", clientID),
			zap.Int64("ActivityContextId", contextID),
			zap.Int64("RequestId", requestID),
			zap.Int("ProcessId", os.Getpid()))

		// wait for ActivityInvokeReply
		result := <-op.GetChannel()
		switch s := result.(type) {
		case error:
			Logger.Error("Activity Failed With Error",
				zap.String("Activity", activityName),
				zap.Int64("ClientId", clientID),
				zap.Int64("ActivityContextId", contextID),
				zap.Int64("RequestId", requestID),
				zap.Error(s),
				zap.Int("ProcessId", os.Getpid()))

			return nil, s

		case []byte:
			Logger.Info("Activity Completed Successfully",
				zap.String("Activity", activityName),
				zap.Int64("ClientId", clientID),
				zap.Int64("ActivityContextId", contextID),
				zap.Int64("RequestId", requestID),
				zap.ByteString("Result", s),
				zap.Int("ProcessId", os.Getpid()))

			return s, nil

		default:
			Logger.Error("Activity Result unexpected",
				zap.String("Activity", activityName),
				zap.Int64("ClientId", clientID),
				zap.Int64("ActivityContextId", contextID),
				zap.Int64("RequestId", requestID),
				zap.Any("Result", s),
				zap.Int("ProcessId", os.Getpid()))

			return nil, fmt.Errorf("unexpected result type %v.  result must be an error or []byte", reflect.TypeOf(s))
		}
	}

	clientHelper.ActivityRegister(workerID, activityFunc, activityName)
	Logger.Debug("Activity Successfully Registered", zap.String("ActivityName", activityName))

	reply.Build(nil)

	return reply
}

func handleActivityExecuteRequest(requestCtx context.Context, request *messages.ActivityExecuteRequest) messages.IProxyReply {
	contextID := request.GetContextID()
	clientID := request.GetClientID()
	requestID := request.GetRequestID()
	activityName := *request.GetActivity()
	Logger.Debug("ActivityExecuteRequest Received",
		zap.String("ActivityName", activityName),
		zap.Int64("ClientId", clientID),
		zap.Int64("ContextId", contextID),
		zap.Int64("RequestId", requestID),
		zap.Int("ProcessId", os.Getpid()))

	// new ActivityExecuteReply
	reply := messages.CreateReplyMessage(request)

	// get the contextID and the corresponding context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	// get the activity options
	var opts workflow.ActivityOptions
	if v := request.GetOptions(); v != nil {
		opts = *v
	}

	// get the activity options, the context,
	// and set the activity options on the context
	ctx := workflow.WithActivityOptions(wectx.GetContext(), opts)
	//ctx = workflow.WithWorkflowNamespace(ctx, *request.GetNamespace())
	future := workflow.ExecuteActivity(ctx, activityName, request.GetArgs())

	// execute the activity
	var result []byte
	if err := future.Get(ctx, &result); err != nil {
		reply.Build(err)
		return reply
	}

	reply.Build(nil, result)

	return reply
}

func handleActivityStartRequest(requestCtx context.Context, request *messages.ActivityStartRequest) messages.IProxyReply {
	contextID := request.GetContextID()
	clientID := request.GetClientID()
	requestID := request.GetRequestID()
	activityID := request.GetActivityID()
	activity := *request.GetActivity()
	Logger.Debug("ActivityStartRequest Received",
		zap.String("Activity", activity),
		zap.Int64("ActivityId", activityID),
		zap.Int64("ClientId", clientID),
		zap.Int64("ContextId", contextID),
		zap.Int64("RequestId", requestID),
		zap.Int("ProcessId", os.Getpid()))

	// new ActivityStartReply
	reply := messages.CreateReplyMessage(request)

	// get the contextID and the corresponding context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	// get the activity options
	var opts workflow.ActivityOptions
	if v := request.GetOptions(); v != nil {
		opts = *v
	}

	// get the activity options, the context,
	// and set the activity options on the context
	// and set cancelation
	var cancel workflow.CancelFunc
	ctx := workflow.WithActivityOptions(wectx.GetContext(), opts)
	//ctx = workflow.WithWorkflowNamespace(ctx, *request.GetNamespace())
	ctx, cancel = workflow.WithCancel(ctx)

	//execute workflow
	future := workflow.ExecuteActivity(ctx, activity, request.GetArgs())

	// add to workflow context map
	_ = wectx.AddActivity(activityID, *proxyworkflow.NewActivity(future, cancel))

	reply.Build(nil)

	return reply
}

func handleActivityGetResultRequest(requestCtx context.Context, request *messages.ActivityGetResultRequest) messages.IProxyReply {
	contextID := request.GetContextID()
	clientID := request.GetClientID()
	requestID := request.GetRequestID()
	activityID := request.GetActivityID()
	Logger.Debug("ActivityGetResultRequest Received",
		zap.Int64("ActivityId", activityID),
		zap.Int64("ClientId", clientID),
		zap.Int64("ContextId", contextID),
		zap.Int64("RequestId", requestID),
		zap.Int("ProcessId", os.Getpid()))

	// new ActivityGetResultReply
	reply := messages.CreateReplyMessage(request)

	// get the contextID and the corresponding context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	activity := wectx.GetActivity(activityID)
	if activity.GetFuture() == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}
	defer wectx.RemoveActivity(activityID)

	// execute the activity
	var result []byte
	if err := activity.GetFuture().Get(wectx.GetContext(), &result); err != nil {
		reply.Build(err)
		return reply
	}
	reply.Build(nil, result)

	return reply
}

func handleActivityHasHeartbeatDetailsRequest(requestCtx context.Context, request *messages.ActivityHasHeartbeatDetailsRequest) messages.IProxyReply {
	Logger.Debug("ActivityHasHeartbeatDetailsRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()))

	// new ActivityHasHeartbeatDetailsReply
	reply := messages.CreateReplyMessage(request)

	actx := ActivityContexts.Get(request.GetContextID())
	if actx == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	reply.Build(nil, activity.HasHeartbeatDetails(actx.GetContext()))

	return reply
}

func handleActivityGetHeartbeatDetailsRequest(requestCtx context.Context, request *messages.ActivityGetHeartbeatDetailsRequest) messages.IProxyReply {
	Logger.Debug("ActivityGetHeartbeatDetailsRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()))

	// new ActivityGetHeartbeatDetailsReply
	reply := messages.CreateReplyMessage(request)

	// get the activity context
	actx := ActivityContexts.Get(request.GetContextID())
	if actx == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	// get the activity heartbeat details
	var details []byte
	err := activity.GetHeartbeatDetails(actx.GetContext(), &details)
	if err != nil {
		reply.Build(err)
		return reply
	}
	reply.Build(nil, details)

	return reply
}

func handleActivityRecordHeartbeatRequest(requestCtx context.Context, request *messages.ActivityRecordHeartbeatRequest) messages.IProxyReply {
	clientID := request.GetClientID()
	contextID := request.GetContextID()
	Logger.Debug("ActivityRecordHeartbeatRequest Received",
		zap.Int64("ClientId", clientID),
		zap.Int64("ContextId", contextID),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()))

	// new ActivityRecordHeartbeatReply
	reply := messages.CreateReplyMessage(request)

	clientHelper := Clients.Get(clientID)
	if clientHelper == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	ctx, cancel := context.WithTimeout(requestCtx, clientHelper.GetClientTimeout())
	defer cancel()

	// check to see if external or internal
	// record heartbeat
	var err error
	details := request.GetDetails()
	if request.GetTaskToken() == nil {
		if request.GetActivityID() == nil {
			actx := ActivityContexts.Get(contextID)
			if actx == nil {
				reply.Build(internal.ErrEntityNotExist)
				return reply
			}

			activity.RecordHeartbeat(ActivityContexts.Get(contextID).GetContext(), details)

		} else {
			err = clientHelper.RecordActivityHeartbeatByID(
				ctx,
				*request.GetNamespace(),
				*request.GetWorkflowID(),
				*request.GetRunID(),
				*request.GetActivityID(),
				details)
		}

	} else {
		err = clientHelper.RecordActivityHeartbeat(
			ctx,
			request.GetTaskToken(),
			*request.GetNamespace(),
			details)
	}

	if err != nil {
		reply.Build(err)
		return reply
	}

	reply.Build(nil)

	return reply
}

func handleActivityGetInfoRequest(requestCtx context.Context, request *messages.ActivityGetInfoRequest) messages.IProxyReply {
	contextID := request.GetContextID()
	Logger.Debug("ActivityGetInfoRequest Received",
		zap.Int64("ClientId", request.GetClientID()),
		zap.Int64("ActivityContextId", contextID),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()))

	// new ActivityGetInfoReply
	reply := messages.CreateReplyMessage(request)

	// get the activity context
	actx := ActivityContexts.Get(contextID)
	if actx == nil {
		reply.Build(internal.ErrConnection)
		return reply
	}

	// get info
	// build the reply
	info := activity.GetInfo(actx.GetContext())

	reply.Build(nil, &info)

	return reply
}

func handleActivityCompleteRequest(requestCtx context.Context, request *messages.ActivityCompleteRequest) messages.IProxyReply {
	clientID := request.GetClientID()
	Logger.Debug("ActivityCompleteRequest Received",
		zap.Int64("ClientId", clientID),
		zap.Int64("RequestId", request.GetRequestID()),
		zap.Int("ProcessId", os.Getpid()))

	// new ActivityCompleteReply
	reply := messages.CreateReplyMessage(request)

	clientHelper := Clients.Get(clientID)
	if clientHelper == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	// create the context
	ctx, cancel := context.WithTimeout(requestCtx, clientHelper.GetClientTimeout())
	defer cancel()

	// check the task token
	// and complete activity
	var err error
	taskToken := request.GetTaskToken()

	if taskToken == nil {
		err = clientHelper.CompleteActivityByID(
			ctx,
			*request.GetNamespace(),
			*request.GetWorkflowID(),
			*request.GetRunID(),
			*request.GetActivityID(),
			request.GetResult(),
			request.GetError())

	} else {
		err = clientHelper.CompleteActivity(
			ctx,
			taskToken,
			*request.GetNamespace(),
			request.GetResult(),
			request.GetError())
	}

	if err != nil {
		reply.Build(err)
		return reply
	}

	reply.Build(nil)

	return reply
}

func handleActivityExecuteLocalRequest(requestCtx context.Context, request *messages.ActivityExecuteLocalRequest) messages.IProxyReply {
	contextID := request.GetContextID()
	clientID := request.GetClientID()
	requestID := request.GetRequestID()
	activityTypeID := request.GetActivityTypeID()
	Logger.Debug("ActivityExecuteLocalRequest Received",
		zap.Int64("ActivityTypeId", activityTypeID),
		zap.Int64("ClientId", clientID),
		zap.Int64("RequestId", requestID),
		zap.Int64("ContextId", contextID),
		zap.Int("ProcessId", os.Getpid()))

	// new ActivityExecuteLocalReply
	reply := messages.CreateReplyMessage(request)

	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	// the local activity function
	localActivityFunc := func(ctx context.Context, input []byte) ([]byte, error) {
		actx := proxyactivity.NewActivityContext(ctx)
		activityContextID := ActivityContexts.Add(proxyactivity.NextContextID(), actx)

		// Send a ActivityInvokeLocalRequest to the Neon.Temporal Lib
		// temporal-client
		requestID := NextRequestID()
		activityInvokeLocalRequest := messages.NewActivityInvokeLocalRequest()
		activityInvokeLocalRequest.SetRequestID(requestID)
		activityInvokeLocalRequest.SetContextID(contextID)
		activityInvokeLocalRequest.SetArgs(input)
		activityInvokeLocalRequest.SetActivityTypeID(activityTypeID)
		activityInvokeLocalRequest.SetActivityContextID(activityContextID)
		activityInvokeLocalRequest.SetClientID(clientID)

		// create the Operation for this request and add it to the operations map
		op := NewOperation(requestID, activityInvokeLocalRequest)
		op.SetChannel(make(chan interface{}))
		op.SetContextID(activityContextID)
		Operations.Add(requestID, op)

		// send the request
		go sendMessage(activityInvokeLocalRequest)

		Logger.Debug("ActivityInvokeLocalRequest sent",
			zap.Int64("ActivityTypeId", activityTypeID),
			zap.Int64("ClientId", clientID),
			zap.Int64("ContextId", contextID),
			zap.Int64("ActivityContextId", activityContextID),
			zap.Int64("RequestId", requestID),
			zap.Int("ProcessId", os.Getpid()))

		// wait for ActivityInvokeReply
		result := <-op.GetChannel()
		switch s := result.(type) {
		case error:
			Logger.Error("Activity Failed With Error",
				zap.Int64("ActivityTypeId", activityTypeID),
				zap.Int64("ClientId", clientID),
				zap.Int64("ContextId", contextID),
				zap.Int64("ActivityContextId", activityContextID),
				zap.Int64("RequestId", requestID),
				zap.Error(s),
				zap.Int("ProcessId", os.Getpid()))

			return nil, s

		case []byte:
			Logger.Info("Activity Successful",
				zap.Int64("ActivityTypeId", activityTypeID),
				zap.Int64("ClientId", clientID),
				zap.Int64("ContextId", contextID),
				zap.Int64("ActivityContextId", activityContextID),
				zap.Int64("RequestId", requestID),
				zap.Any("Result", s),
				zap.Int("ProcessId", os.Getpid()))

			return s, nil

		default:
			Logger.Error("Activity Result Unexpected",
				zap.Int64("ActivityTypeId", activityTypeID),
				zap.Int64("ClientId", clientID),
				zap.Int64("ContextId", contextID),
				zap.Int64("ActivityContextId", activityContextID),
				zap.Int64("RequestId", requestID),
				zap.Any("Result", s),
				zap.Int("ProcessId", os.Getpid()))

			return nil, fmt.Errorf("unexpected result type %v.  result must be an error or []byte", reflect.TypeOf(s))
		}
	}

	// get the activity options
	var opts workflow.LocalActivityOptions
	if v := request.GetOptions(); v != nil {
		opts = *v
	}

	// set the activity options on the context
	// execute local activity
	ctx := workflow.WithLocalActivityOptions(wectx.GetContext(), opts)
	future := workflow.ExecuteLocalActivity(ctx, localActivityFunc, request.GetArgs())

	// Send ACK: Commented out because its no longer needed.
	// op := sendFutureACK(contextID, requestID, clientID)
	// <-op.GetChannel()

	// wait for the future to be unblocked
	var result []byte
	if err := future.Get(ctx, &result); err != nil {
		reply.Build(err)
		return reply
	}

	reply.Build(nil, result)

	return reply
}

func handleActivityStartLocalRequest(requestCtx context.Context, request *messages.ActivityStartLocalRequest) messages.IProxyReply {
	contextID := request.GetContextID()
	clientID := request.GetClientID()
	requestID := request.GetRequestID()
	activityID := request.GetActivityID()
	activityTypeID := request.GetActivityTypeID()
	Logger.Debug("ActivityStartLocalRequest Received",
		zap.Int64("ActivityTypeId", activityTypeID),
		zap.Int64("ActivityId", activityID),
		zap.Int64("ClientId", clientID),
		zap.Int64("ContextId", contextID),
		zap.Int64("RequestId", requestID),
		zap.Int("ProcessId", os.Getpid()))

	// new ActivityStartLocalReply
	reply := messages.CreateReplyMessage(request)

	// get the contextID and the corresponding context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	// the local activity function
	localActivityFunc := func(ctx context.Context, input []byte) ([]byte, error) {
		actx := proxyactivity.NewActivityContext(ctx)
		activityContextID := ActivityContexts.Add(proxyactivity.NextContextID(), actx)

		// Send a ActivityInvokeLocalRequest to the Neon.Temporal Lib
		// temporal-client
		requestID := NextRequestID()
		activityInvokeLocalRequest := messages.NewActivityInvokeLocalRequest()
		activityInvokeLocalRequest.SetRequestID(requestID)
		activityInvokeLocalRequest.SetContextID(contextID)
		activityInvokeLocalRequest.SetArgs(input)
		activityInvokeLocalRequest.SetActivityTypeID(activityTypeID)
		activityInvokeLocalRequest.SetActivityContextID(activityContextID)
		activityInvokeLocalRequest.SetClientID(clientID)

		// create the Operation for this request and add it to the operations map
		op := NewOperation(requestID, activityInvokeLocalRequest)
		op.SetChannel(make(chan interface{}))
		op.SetContextID(activityContextID)
		Operations.Add(requestID, op)

		// send the request
		go sendMessage(activityInvokeLocalRequest)

		Logger.Debug("ActivityInvokeLocalRequest sent",
			zap.Int64("ActivityTypeId", activityTypeID),
			zap.Int64("ClientId", clientID),
			zap.Int64("ContextId", contextID),
			zap.Int64("ActivityContextId", activityContextID),
			zap.Int64("RequestId", requestID),
			zap.Int("ProcessId", os.Getpid()))

		// wait for ActivityInvokeReply
		result := <-op.GetChannel()
		switch s := result.(type) {
		case error:
			Logger.Error("Activity Failed With Error",
				zap.Int64("ActivityTypeId", activityTypeID),
				zap.Int64("ClientId", clientID),
				zap.Int64("ContextId", contextID),
				zap.Int64("ActivityContextId", activityContextID),
				zap.Int64("RequestId", requestID),
				zap.Error(s),
				zap.Int("ProcessId", os.Getpid()))

			return nil, s

		case []byte:
			Logger.Info("Activity Successful",
				zap.Int64("ActivityTypeId", activityTypeID),
				zap.Int64("ClientId", clientID),
				zap.Int64("ContextId", contextID),
				zap.Int64("ActivityContextId", activityContextID),
				zap.Int64("RequestId", requestID),
				zap.Any("Result", s),
				zap.Int("ProcessId", os.Getpid()))

			return s, nil

		default:
			Logger.Error("Activity Result Unexpected",
				zap.Int64("ActivityTypeId", activityTypeID),
				zap.Int64("ClientId", clientID),
				zap.Int64("ContextId", contextID),
				zap.Int64("ActivityContextId", activityContextID),
				zap.Int64("RequestId", requestID),
				zap.Any("Result", s),
				zap.Int("ProcessId", os.Getpid()))

			return nil, fmt.Errorf("unexpected result type %v.  result must be an error or []byte", reflect.TypeOf(s))
		}
	}

	/// get the activity options
	var opts workflow.LocalActivityOptions
	if v := request.GetOptions(); v != nil {
		opts = *v
	}

	/// set the activity options on the context
	// execute local activity
	var cancel workflow.CancelFunc
	ctx := workflow.WithLocalActivityOptions(wectx.GetContext(), opts)
	ctx, cancel = workflow.WithCancel(ctx)
	future := workflow.ExecuteLocalActivity(ctx, localActivityFunc, request.GetArgs())

	// add to workflow context map
	_ = wectx.AddActivity(activityID, *proxyworkflow.NewActivity(future, cancel))
	reply.Build(nil)

	return reply
}

func handleActivityGetLocalResultRequest(requestCtx context.Context, request *messages.ActivityGetLocalResultRequest) messages.IProxyReply {
	contextID := request.GetContextID()
	clientID := request.GetClientID()
	requestID := request.GetRequestID()
	activityID := request.GetActivityID()
	Logger.Debug("ActivityGetLocalResultRequest Received",
		zap.Int64("ActivityId", activityID),
		zap.Int64("ClientId", clientID),
		zap.Int64("ContextId", contextID),
		zap.Int64("RequestId", requestID),
		zap.Int("ProcessId", os.Getpid()))

	// new ActivityGetLocalResultReply
	reply := messages.CreateReplyMessage(request)

	// get the contextID and the corresponding context
	wectx := WorkflowContexts.Get(contextID)
	if wectx == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}

	activity := wectx.GetActivity(activityID)
	if activity.GetFuture() == nil {
		reply.Build(internal.ErrEntityNotExist)
		return reply
	}
	defer wectx.RemoveActivity(activityID)

	var result []byte
	if err := activity.GetFuture().Get(wectx.GetContext(), &result); err != nil {
		reply.Build(err)
		return reply
	}

	reply.Build(nil, result)

	return reply
}
