//-----------------------------------------------------------------------------
// FILE:		reply_builders.go
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
	"time"

	"go.temporal.io/temporal-proto/workflowservice"
	"go.temporal.io/temporal/activity"
	"go.temporal.io/temporal/workflow"

	internal "temporal-proxy/internal"
	"temporal-proxy/internal/messages"
	proxyerror "temporal-proxy/internal/temporal/error"
)

func buildReply(reply messages.IProxyReply, temporalError *proxyerror.TemporalError, values ...interface{}) {

	// check if there is anything in values
	var value interface{}
	if len(values) > 0 {
		value = values[0]
	}

	// handle the messages individually based on their message type
	switch reply.GetType() {

	// -------------------------------------------------------------------------
	// client message types

	// InitializeReply
	case internal.InitializeReply:
		if v, ok := reply.(*messages.InitializeReply); ok {
			buildInitializeReply(v, temporalError)
		}

	// HeartbeatReply
	case internal.HeartbeatReply:
		if v, ok := reply.(*messages.HeartbeatReply); ok {
			buildHeartbeatReply(v, temporalError)
		}

	// CancelReply
	case internal.CancelReply:
		if v, ok := reply.(*messages.CancelReply); ok {
			buildCancelReply(v, temporalError, value)
		}

	// ConnectReply
	case internal.ConnectReply:
		if v, ok := reply.(*messages.ConnectReply); ok {
			buildConnectReply(v, temporalError)
		}

	// ConnectReply
	case internal.DisconnectReply:
		if v, ok := reply.(*messages.DisconnectReply); ok {
			buildDisconnectReply(v, temporalError)
		}

	// NamespaceDescribeReply
	case internal.NamespaceDescribeReply:
		if v, ok := reply.(*messages.NamespaceDescribeReply); ok {
			buildNamespaceDescribeReply(v, temporalError, value)
		}

	// NamespaceRegisterReply
	case internal.NamespaceRegisterReply:
		if v, ok := reply.(*messages.NamespaceRegisterReply); ok {
			buildNamespaceRegisterReply(v, temporalError)
		}

	// NamespaceUpdateReply
	case internal.NamespaceUpdateReply:
		if v, ok := reply.(*messages.NamespaceUpdateReply); ok {
			buildNamespaceUpdateReply(v, temporalError)
		}

	// NamespaceListReply
	case internal.NamespaceListReply:
		if v, ok := reply.(*messages.NamespaceListReply); ok {
			buildNamespaceListReply(v, temporalError, value)
		}

	// TerminateReply
	case internal.TerminateReply:
		if v, ok := reply.(*messages.TerminateReply); ok {
			buildTerminateReply(v, temporalError)
		}

	// NewWorkerReply
	case internal.NewWorkerReply:
		if v, ok := reply.(*messages.NewWorkerReply); ok {
			buildNewWorkerReply(v, temporalError, value)
		}

	// StopWorkerReply
	case internal.StopWorkerReply:
		if v, ok := reply.(*messages.StopWorkerReply); ok {
			buildStopWorkerReply(v, temporalError)
		}

	// PingReply
	case internal.PingReply:
		if v, ok := reply.(*messages.PingReply); ok {
			buildPingReply(v, temporalError)
		}

	// DescribeTaskListReply
	case internal.DescribeTaskListReply:
		if v, ok := reply.(*messages.DescribeTaskListReply); ok {
			buildDescribeTaskListReply(v, temporalError, value)
		}

	// -------------------------------------------------------------------------
	// workflow message types

	// WorkflowExecuteReply
	case internal.WorkflowExecuteReply:
		if v, ok := reply.(*messages.WorkflowExecuteReply); ok {
			buildWorkflowExecuteReply(v, temporalError, value)
		}

	// WorkflowRegisterReply
	case internal.WorkflowRegisterReply:
		if v, ok := reply.(*messages.WorkflowRegisterReply); ok {
			buildWorkflowRegisterReply(v, temporalError)
		}

	// WorkflowCancelReply
	case internal.WorkflowCancelReply:
		if v, ok := reply.(*messages.WorkflowCancelReply); ok {
			buildWorkflowCancelReply(v, temporalError)
		}

	// WorkflowSignalWithStartReply
	case internal.WorkflowSignalWithStartReply:
		if v, ok := reply.(*messages.WorkflowSignalWithStartReply); ok {
			buildWorkflowSignalWithStartReply(v, temporalError, value)
		}

	// WorkflowQueryReply
	case internal.WorkflowQueryReply:
		if v, ok := reply.(*messages.WorkflowQueryReply); ok {
			buildWorkflowQueryReply(v, temporalError, value)
		}

	// WorkflowSetQueryHandlerReply
	case internal.WorkflowSetQueryHandlerReply:
		if v, ok := reply.(*messages.WorkflowSetQueryHandlerReply); ok {
			buildWorkflowSetQueryHandlerReply(v, temporalError)
		}

	// WorkflowSetCacheSizeReply
	case internal.WorkflowSetCacheSizeReply:
		if v, ok := reply.(*messages.WorkflowSetCacheSizeReply); ok {
			buildWorkflowSetCacheSizeReply(v, temporalError)
		}

	// WorkflowMutableReply
	case internal.WorkflowMutableReply:
		if v, ok := reply.(*messages.WorkflowMutableReply); ok {
			buildWorkflowMutableReply(v, temporalError, value)
		}

	// WorkflowTerminateReply
	case internal.WorkflowTerminateReply:
		if v, ok := reply.(*messages.WorkflowTerminateReply); ok {
			buildWorkflowTerminateReply(v, temporalError)
		}

	// WorkflowDescribeExecutionReply
	case internal.WorkflowDescribeExecutionReply:
		if v, ok := reply.(*messages.WorkflowDescribeExecutionReply); ok {
			buildWorkflowDescribeExecutionReply(v, temporalError, value)
		}

	// WorkflowGetResultReply
	case internal.WorkflowGetResultReply:
		if v, ok := reply.(*messages.WorkflowGetResultReply); ok {
			buildWorkflowGetResultReply(v, temporalError, value)
		}

	// WorkflowHasLastResultReply
	case internal.WorkflowHasLastResultReply:
		if v, ok := reply.(*messages.WorkflowHasLastResultReply); ok {
			buildWorkflowHasLastResultReply(v, temporalError, value)
		}

	// WorkflowGetLastResultReply
	case internal.WorkflowGetLastResultReply:
		if v, ok := reply.(*messages.WorkflowGetLastResultReply); ok {
			buildWorkflowGetLastResultReply(v, temporalError, value)
		}

	// WorkflowDisconnectContextReply
	case internal.WorkflowDisconnectContextReply:
		if v, ok := reply.(*messages.WorkflowDisconnectContextReply); ok {
			buildWorkflowDisconnectContextReply(v, temporalError)
		}

	// WorkflowGetTimeReply
	case internal.WorkflowGetTimeReply:
		if v, ok := reply.(*messages.WorkflowGetTimeReply); ok {
			buildWorkflowGetTimeReply(v, temporalError, value)
		}

	// WorkflowSleepReply
	case internal.WorkflowSleepReply:
		if v, ok := reply.(*messages.WorkflowSleepReply); ok {
			buildWorkflowSleepReply(v, temporalError)
		}

	// WorkflowExecuteChildReply
	case internal.WorkflowExecuteChildReply:
		if v, ok := reply.(*messages.WorkflowExecuteChildReply); ok {
			buildWorkflowExecuteChildReply(v, temporalError, value)
		}

	// WorkflowWaitForChildReply
	case internal.WorkflowWaitForChildReply:
		if v, ok := reply.(*messages.WorkflowWaitForChildReply); ok {
			buildWorkflowWaitForChildReply(v, temporalError, value)
		}

	// WorkflowSignalChildReply
	case internal.WorkflowSignalChildReply:
		if v, ok := reply.(*messages.WorkflowSignalChildReply); ok {
			buildWorkflowSignalChildReply(v, temporalError, value)
		}

	// WorkflowCancelChildReply
	case internal.WorkflowCancelChildReply:
		if v, ok := reply.(*messages.WorkflowCancelChildReply); ok {
			buildWorkflowCancelChildReply(v, temporalError)
		}

	// WorkflowSignalReply
	case internal.WorkflowSignalReply:
		if v, ok := reply.(*messages.WorkflowSignalReply); ok {
			buildWorkflowSignalReply(v, temporalError)
		}

	// WorkflowSignalSubscribeReply
	case internal.WorkflowSignalSubscribeReply:
		if v, ok := reply.(*messages.WorkflowSignalSubscribeReply); ok {
			buildWorkflowSignalSubscribeReply(v, temporalError)
		}

	// WorkflowGetVersionReply
	case internal.WorkflowGetVersionReply:
		if v, ok := reply.(*messages.WorkflowGetVersionReply); ok {
			buildWorkflowGetVersionReply(v, temporalError, value)
		}

	// WorkflowQueueNewReply
	case internal.WorkflowQueueNewReply:
		if v, ok := reply.(*messages.WorkflowQueueNewReply); ok {
			buildWorkflowQueueNewReply(v, temporalError)
		}

	// WorkflowQueueWriteReply
	case internal.WorkflowQueueWriteReply:
		if v, ok := reply.(*messages.WorkflowQueueWriteReply); ok {
			buildWorkflowQueueWriteReply(v, temporalError, value)
		}

	// WorkflowQueueReadReply
	case internal.WorkflowQueueReadReply:
		if v, ok := reply.(*messages.WorkflowQueueReadReply); ok {
			buildWorkflowQueueReadReply(v, temporalError, value)
		}

	// WorkflowQueueCloseReply
	case internal.WorkflowQueueCloseReply:
		if v, ok := reply.(*messages.WorkflowQueueCloseReply); ok {
			buildWorkflowQueueCloseReply(v, temporalError)
		}

	// -------------------------------------------------------------------------
	// activity message types

	// ActivityRegisterReply
	case internal.ActivityRegisterReply:
		if v, ok := reply.(*messages.ActivityRegisterReply); ok {
			buildActivityRegisterReply(v, temporalError)
		}

	// ActivityExecuteReply
	case internal.ActivityExecuteReply:
		if v, ok := reply.(*messages.ActivityExecuteReply); ok {
			buildActivityExecuteReply(v, temporalError, value)
		}

	// ActivityHasHeartbeatDetailsReply
	case internal.ActivityHasHeartbeatDetailsReply:
		if v, ok := reply.(*messages.ActivityHasHeartbeatDetailsReply); ok {
			buildActivityHasHeartbeatDetailsReply(v, temporalError, value)
		}

	// ActivityGetHeartbeatDetailsReply
	case internal.ActivityGetHeartbeatDetailsReply:
		if v, ok := reply.(*messages.ActivityGetHeartbeatDetailsReply); ok {
			buildActivityGetHeartbeatDetailsReply(v, temporalError, value)
		}

	// ActivityRecordHeartbeatReply
	case internal.ActivityRecordHeartbeatReply:
		if v, ok := reply.(*messages.ActivityRecordHeartbeatReply); ok {
			buildActivityRecordHeartbeatReply(v, temporalError, value)
		}

	// ActivityGetInfoReply
	case internal.ActivityGetInfoReply:
		if v, ok := reply.(*messages.ActivityGetInfoReply); ok {
			buildActivityGetInfoReply(v, temporalError, value)
		}

	// ActivityCompleteReply
	case internal.ActivityCompleteReply:
		if v, ok := reply.(*messages.ActivityCompleteReply); ok {
			buildActivityCompleteReply(v, temporalError)
		}

	// ActivityExecuteLocalReply
	case internal.ActivityExecuteLocalReply:
		if v, ok := reply.(*messages.ActivityExecuteLocalReply); ok {
			buildActivityExecuteLocalReply(v, temporalError, value)
		}

	// ActivityStartReply
	case internal.ActivityStartReply:
		if v, ok := reply.(*messages.ActivityStartReply); ok {
			buildActivityStartReply(v, temporalError)
		}

	// ActivityGetResultReply
	case internal.ActivityGetResultReply:
		if v, ok := reply.(*messages.ActivityGetResultReply); ok {
			buildActivityGetResultReply(v, temporalError, value)
		}

	// ActivityStartLocalReply
	case internal.ActivityStartLocalReply:
		if v, ok := reply.(*messages.ActivityStartLocalReply); ok {
			buildActivityStartLocalReply(v, temporalError)
		}

	// ActivityGetLocalResultReply
	case internal.ActivityGetLocalResultReply:
		if v, ok := reply.(*messages.ActivityGetLocalResultReply); ok {
			buildActivityGetLocalResultReply(v, temporalError, value)
		}

	// Undefined message type
	// This should never happen.
	default:
		err := fmt.Errorf("Error building reply for message type %s", reply.GetType())
		panic(err)
	}
}

// -------------------------------------------------------------------------
// Client message builders

func buildCancelReply(reply *messages.CancelReply, temporalError *proxyerror.TemporalError, wasCancelled ...interface{}) {
	reply.SetError(temporalError)

	if len(wasCancelled) > 0 {
		if v, ok := wasCancelled[0].(bool); ok {
			reply.SetWasCancelled(v)
		}
	}
}

func buildConnectReply(reply *messages.ConnectReply, temporalError *proxyerror.TemporalError) {
	reply.SetError(temporalError)
}

func buildDisconnectReply(reply *messages.DisconnectReply, temporalError *proxyerror.TemporalError) {
	reply.SetError(temporalError)
}

func buildNamespaceDescribeReply(reply *messages.NamespaceDescribeReply, temporalError *proxyerror.TemporalError, describeNamespaceResponse ...interface{}) {
	reply.SetError(temporalError)
	if len(describeNamespaceResponse) > 0 {
		if v, ok := describeNamespaceResponse[0].(*workflowservice.DescribeNamespaceResponse); ok {
			reply.SetNamespaceInfoName(&v.NamespaceInfo.Name)
			reply.SetNamespaceInfoDescription(&v.NamespaceInfo.Description)
			reply.SetNamespaceInfoStatus(v.NamespaceInfo.Status)
			reply.SetConfigurationEmitMetrics(v.Configuration.EmitMetric.Value)
			reply.SetConfigurationRetentionDays(v.Configuration.GetWorkflowExecutionRetentionPeriodInDays())
			reply.SetNamespaceInfoOwnerEmail(&v.NamespaceInfo.OwnerEmail)
		}
	}
}

func buildNamespaceRegisterReply(reply *messages.NamespaceRegisterReply, temporalError *proxyerror.TemporalError) {
	reply.SetError(temporalError)
}

func buildNamespaceUpdateReply(reply *messages.NamespaceUpdateReply, temporalError *proxyerror.TemporalError) {
	reply.SetError(temporalError)
}

func buildNamespaceListReply(reply *messages.NamespaceListReply, temporalError *proxyerror.TemporalError, listNamespacesResponse ...interface{}) {
	reply.SetError(temporalError)
	if len(listNamespacesResponse) > 0 {
		if v, ok := listNamespacesResponse[0].(*workflowservice.ListNamespacesResponse); ok {
			reply.SetNamespaces(v.Namespaces)
			reply.SetNextPageToken(v.NextPageToken)
		}
	}
}

func buildHeartbeatReply(reply *messages.HeartbeatReply, temporalError *proxyerror.TemporalError) {
	reply.SetError(temporalError)
}

func buildInitializeReply(reply *messages.InitializeReply, temporalError *proxyerror.TemporalError) {
	reply.SetError(temporalError)
}

func buildTerminateReply(reply *messages.TerminateReply, temporalError *proxyerror.TemporalError) {
	reply.SetError(temporalError)
}

func buildNewWorkerReply(reply *messages.NewWorkerReply, temporalError *proxyerror.TemporalError, workerID ...interface{}) {
	reply.SetError(temporalError)
	if len(workerID) > 0 {
		if v, ok := workerID[0].(int64); ok {
			reply.SetWorkerID(v)
		}
	}
}

func buildStopWorkerReply(reply *messages.StopWorkerReply, temporalError *proxyerror.TemporalError) {
	reply.SetError(temporalError)
}

func buildPingReply(reply *messages.PingReply, temporalError *proxyerror.TemporalError) {
	reply.SetError(temporalError)
}

func buildDescribeTaskListReply(reply *messages.DescribeTaskListReply, temporalError *proxyerror.TemporalError, response ...interface{}) {
	reply.SetError(temporalError)
	if len(response) > 0 {
		if v, ok := response[0].(*workflowservice.DescribeTaskListResponse); ok {
			reply.SetResult(v)
		}
	}
}

// -------------------------------------------------------------------------
// Workflow message builders

func buildWorkflowRegisterReply(reply *messages.WorkflowRegisterReply, temporalError *proxyerror.TemporalError) {
	reply.SetError(temporalError)
}

func buildWorkflowExecuteReply(reply *messages.WorkflowExecuteReply, temporalError *proxyerror.TemporalError, execution ...interface{}) {
	reply.SetError(temporalError)
	if len(execution) > 0 {
		if v, ok := execution[0].(*workflow.Execution); ok {
			reply.SetExecution(v)
		}
	}
}

func buildWorkflowCancelReply(reply *messages.WorkflowCancelReply, temporalError *proxyerror.TemporalError) {
	reply.SetError(temporalError)
}

func buildWorkflowTerminateReply(reply *messages.WorkflowTerminateReply, temporalError *proxyerror.TemporalError) {
	reply.SetError(temporalError)
}

func buildWorkflowSignalSubscribeReply(reply *messages.WorkflowSignalSubscribeReply, temporalError *proxyerror.TemporalError) {
	reply.SetError(temporalError)
}

func buildWorkflowSignalReply(reply *messages.WorkflowSignalReply, temporalError *proxyerror.TemporalError) {
	reply.SetError(temporalError)
}

func buildWorkflowSignalWithStartReply(reply *messages.WorkflowSignalWithStartReply, temporalError *proxyerror.TemporalError, execution ...interface{}) {
	reply.SetError(temporalError)
	if len(execution) > 0 {
		if v, ok := execution[0].(*workflow.Execution); ok {
			reply.SetExecution(v)
		}
	}
}

func buildWorkflowSetCacheSizeReply(reply *messages.WorkflowSetCacheSizeReply, temporalError *proxyerror.TemporalError) {
	reply.SetError(temporalError)
}

func buildWorkflowQueryReply(reply *messages.WorkflowQueryReply, temporalError *proxyerror.TemporalError, result ...interface{}) {
	reply.SetError(temporalError)
	if len(result) > 0 {
		if v, ok := result[0].([]byte); ok {
			reply.SetResult(v)
		}
	}
}

func buildWorkflowMutableReply(reply *messages.WorkflowMutableReply, temporalError *proxyerror.TemporalError, result ...interface{}) {
	reply.SetError(temporalError)
	if len(result) > 0 {
		if v, ok := result[0].([]byte); ok {
			reply.SetResult(v)
		}
	}
}

func buildWorkflowDescribeExecutionReply(reply *messages.WorkflowDescribeExecutionReply, temporalError *proxyerror.TemporalError, description ...interface{}) {
	reply.SetError(temporalError)
	if len(description) > 0 {
		if v, ok := description[0].(*workflowservice.DescribeWorkflowExecutionResponse); ok {
			reply.SetDetails(v)
		}
	}
}

func buildWorkflowGetResultReply(reply *messages.WorkflowGetResultReply, temporalError *proxyerror.TemporalError, result ...interface{}) {
	reply.SetError(temporalError)
	if len(result) > 0 {
		if v, ok := result[0].([]byte); ok {
			reply.SetResult(v)
		}
	}
}

func buildWorkflowHasLastResultReply(reply *messages.WorkflowHasLastResultReply, temporalError *proxyerror.TemporalError, hasResult ...interface{}) {
	reply.SetError(temporalError)
	if len(hasResult) > 0 {
		if v, ok := hasResult[0].(bool); ok {
			reply.SetHasResult(v)
		}
	}
}

func buildWorkflowGetLastResultReply(reply *messages.WorkflowGetLastResultReply, temporalError *proxyerror.TemporalError, result ...interface{}) {
	reply.SetError(temporalError)
	if len(result) > 0 {
		if v, ok := result[0].([]byte); ok {
			reply.SetResult(v)
		}
	}
}

func buildWorkflowDisconnectContextReply(reply *messages.WorkflowDisconnectContextReply, temporalError *proxyerror.TemporalError) {
	reply.SetError(temporalError)
}

func buildWorkflowGetTimeReply(reply *messages.WorkflowGetTimeReply, temporalError *proxyerror.TemporalError, t ...interface{}) {
	reply.SetError(temporalError)
	if len(t) > 0 {
		if v, ok := t[0].(time.Time); ok {
			reply.SetTime(v)
		}
	}
}

func buildWorkflowSleepReply(reply *messages.WorkflowSleepReply, temporalError *proxyerror.TemporalError) {
	reply.SetError(temporalError)
}

func buildWorkflowExecuteChildReply(reply *messages.WorkflowExecuteChildReply, temporalError *proxyerror.TemporalError, childInfo ...interface{}) {
	reply.SetError(temporalError)
	if len(childInfo) > 0 {
		if v, ok := childInfo[0].([]interface{}); ok {
			if _v, _ok := v[0].(int64); _ok {
				reply.SetChildID(_v)
			}
			if _v, _ok := v[1].(*workflow.Execution); _ok {
				reply.SetExecution(_v)
			}
		}
	}
}

func buildWorkflowWaitForChildReply(reply *messages.WorkflowWaitForChildReply, temporalError *proxyerror.TemporalError, result ...interface{}) {
	reply.SetError(temporalError)
	if len(result) > 0 {
		if v, ok := result[0].([]byte); ok {
			reply.SetResult(v)
		}
	}
}

func buildWorkflowSignalChildReply(reply *messages.WorkflowSignalChildReply, temporalError *proxyerror.TemporalError, result ...interface{}) {
	reply.SetError(temporalError)
	if len(result) > 0 {
		if v, ok := result[0].([]byte); ok {
			reply.SetResult(v)
		}
	}
}

func buildWorkflowCancelChildReply(reply *messages.WorkflowCancelChildReply, temporalError *proxyerror.TemporalError) {
	reply.SetError(temporalError)
}

func buildWorkflowGetVersionReply(reply *messages.WorkflowGetVersionReply, temporalError *proxyerror.TemporalError, result ...interface{}) {
	reply.SetError(temporalError)
	if len(result) > 0 {
		if v, ok := result[0].(workflow.Version); ok {
			reply.SetVersion(int32(v))
		}
	}
}

func buildWorkflowSetQueryHandlerReply(reply *messages.WorkflowSetQueryHandlerReply, temporalError *proxyerror.TemporalError) {
	reply.SetError(temporalError)
}

func buildWorkflowQueueNewReply(reply *messages.WorkflowQueueNewReply, temporalError *proxyerror.TemporalError) {
	reply.SetError(temporalError)
}

func buildWorkflowQueueWriteReply(reply *messages.WorkflowQueueWriteReply, temporalError *proxyerror.TemporalError, isFull ...interface{}) {
	reply.SetError(temporalError)
	if len(isFull) > 0 {
		if v, ok := isFull[0].(bool); ok {
			reply.SetIsFull(v)
		}
	}
}

func buildWorkflowQueueReadReply(reply *messages.WorkflowQueueReadReply, temporalError *proxyerror.TemporalError, values ...interface{}) {
	reply.SetError(temporalError)
	if len(values) > 0 {
		if v, ok := values[0].([]interface{}); ok {
			if _v, _ok := v[0].([]byte); _ok {
				reply.SetData(_v)
			}
			if _v, _ok := v[1].(bool); _ok {
				reply.SetIsClosed(_v)
			}
		}
	}
}

func buildWorkflowQueueCloseReply(reply *messages.WorkflowQueueCloseReply, temporalError *proxyerror.TemporalError) {
	reply.SetError(temporalError)
}

// -------------------------------------------------------------------------
// Activity message builders

func buildActivityRegisterReply(reply *messages.ActivityRegisterReply, temporalError *proxyerror.TemporalError) {
	reply.SetError(temporalError)
}

func buildActivityExecuteReply(reply *messages.ActivityExecuteReply, temporalError *proxyerror.TemporalError, result ...interface{}) {
	reply.SetError(temporalError)
	if len(result) > 0 {
		if v, ok := result[0].([]byte); ok {
			reply.SetResult(v)
		}
	}
}

func buildActivityHasHeartbeatDetailsReply(reply *messages.ActivityHasHeartbeatDetailsReply, temporalError *proxyerror.TemporalError, hasDetails ...interface{}) {
	reply.SetError(temporalError)
	if len(hasDetails) > 0 {
		if v, ok := hasDetails[0].(bool); ok {
			reply.SetHasDetails(v)
		}
	}
}

func buildActivityGetHeartbeatDetailsReply(reply *messages.ActivityGetHeartbeatDetailsReply, temporalError *proxyerror.TemporalError, details ...interface{}) {
	reply.SetError(temporalError)
	if len(details) > 0 {
		if v, ok := details[0].([]byte); ok {
			reply.SetDetails(v)
		}
	}
}

func buildActivityRecordHeartbeatReply(reply *messages.ActivityRecordHeartbeatReply, temporalError *proxyerror.TemporalError, details ...interface{}) {
	reply.SetError(temporalError)
	if len(details) > 0 {
		if v, ok := details[0].([]byte); ok {
			reply.SetDetails(v)
		}
	}
}

func buildActivityGetInfoReply(reply *messages.ActivityGetInfoReply, temporalError *proxyerror.TemporalError, info ...interface{}) {
	reply.SetError(temporalError)
	if len(info) > 0 {
		if v, ok := info[0].(*activity.Info); ok {
			reply.SetInfo(v)
		}
	}
}

func buildActivityCompleteReply(reply *messages.ActivityCompleteReply, temporalError *proxyerror.TemporalError) {
	reply.SetError(temporalError)
}

func buildActivityExecuteLocalReply(reply *messages.ActivityExecuteLocalReply, temporalError *proxyerror.TemporalError, result ...interface{}) {
	reply.SetError(temporalError)
	if len(result) > 0 {
		if v, ok := result[0].([]byte); ok {
			reply.SetResult(v)
		}
	}
}

func buildActivityStartReply(reply *messages.ActivityStartReply, temporalError *proxyerror.TemporalError) {
	reply.SetError(temporalError)
}

func buildActivityGetResultReply(reply *messages.ActivityGetResultReply, temporalError *proxyerror.TemporalError, result ...interface{}) {
	reply.SetError(temporalError)
	if len(result) > 0 {
		if v, ok := result[0].([]byte); ok {
			reply.SetResult(v)
		}
	}
}

func buildActivityStartLocalReply(reply *messages.ActivityStartLocalReply, temporalError *proxyerror.TemporalError) {
	reply.SetError(temporalError)
}

func buildActivityGetLocalResultReply(reply *messages.ActivityGetLocalResultReply, temporalError *proxyerror.TemporalError, result ...interface{}) {
	reply.SetError(temporalError)
	if len(result) > 0 {
		if v, ok := result[0].([]byte); ok {
			reply.SetResult(v)
		}
	}
}
