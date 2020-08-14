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
	"reflect"
	"time"

	cadenceshared "go.uber.org/cadence/.gen/go/shared"
	"go.uber.org/cadence/activity"
	"go.uber.org/cadence/workflow"

	internal "github.com/cadence-proxy/internal"
	"github.com/cadence-proxy/internal/messages"
)

func buildReply(reply messages.IProxyReply, err error, values ...interface{}) {

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
			buildInitializeReply(v, err)
		}

	// HeartbeatReply
	case internal.HeartbeatReply:
		if v, ok := reply.(*messages.HeartbeatReply); ok {
			buildHeartbeatReply(v, err)
		}

	// CancelReply
	case internal.CancelReply:
		if v, ok := reply.(*messages.CancelReply); ok {
			buildCancelReply(v, err, value)
		}

	// ConnectReply
	case internal.ConnectReply:
		if v, ok := reply.(*messages.ConnectReply); ok {
			buildConnectReply(v, err)
		}

	// ConnectReply
	case internal.DisconnectReply:
		if v, ok := reply.(*messages.DisconnectReply); ok {
			buildDisconnectReply(v, err)
		}

	// DomainDescribeReply
	case internal.DomainDescribeReply:
		if v, ok := reply.(*messages.DomainDescribeReply); ok {
			buildDomainDescribeReply(v, err, value)
		}

	// DomainRegisterReply
	case internal.DomainRegisterReply:
		if v, ok := reply.(*messages.DomainRegisterReply); ok {
			buildDomainRegisterReply(v, err)
		}

	// DomainUpdateReply
	case internal.DomainUpdateReply:
		if v, ok := reply.(*messages.DomainUpdateReply); ok {
			buildDomainUpdateReply(v, err)
		}

	// DomainListReply
	case internal.DomainListReply:
		if v, ok := reply.(*messages.DomainListReply); ok {
			buildDomainListReply(v, err, value)
		}

	// TerminateReply
	case internal.TerminateReply:
		if v, ok := reply.(*messages.TerminateReply); ok {
			buildTerminateReply(v, err)
		}

	// NewWorkerReply
	case internal.NewWorkerReply:
		if v, ok := reply.(*messages.NewWorkerReply); ok {
			buildNewWorkerReply(v, err, value)
		}

	// StopWorkerReply
	case internal.StopWorkerReply:
		if v, ok := reply.(*messages.StopWorkerReply); ok {
			buildStopWorkerReply(v, err)
		}

	// PingReply
	case internal.PingReply:
		if v, ok := reply.(*messages.PingReply); ok {
			buildPingReply(v, err)
		}

	// DescribeTaskListReply
	case internal.DescribeTaskListReply:
		if v, ok := reply.(*messages.DescribeTaskListReply); ok {
			buildDescribeTaskListReply(v, err, value)
		}

	// -------------------------------------------------------------------------
	// workflow message types

	// WorkflowExecuteReply
	case internal.WorkflowExecuteReply:
		if v, ok := reply.(*messages.WorkflowExecuteReply); ok {
			buildWorkflowExecuteReply(v, err, value)
		}

	// WorkflowRegisterReply
	case internal.WorkflowRegisterReply:
		if v, ok := reply.(*messages.WorkflowRegisterReply); ok {
			buildWorkflowRegisterReply(v, err)
		}

	// WorkflowCancelReply
	case internal.WorkflowCancelReply:
		if v, ok := reply.(*messages.WorkflowCancelReply); ok {
			buildWorkflowCancelReply(v, err)
		}

	// WorkflowSignalWithStartReply
	case internal.WorkflowSignalWithStartReply:
		if v, ok := reply.(*messages.WorkflowSignalWithStartReply); ok {
			buildWorkflowSignalWithStartReply(v, err, value)
		}

	// WorkflowQueryReply
	case internal.WorkflowQueryReply:
		if v, ok := reply.(*messages.WorkflowQueryReply); ok {
			buildWorkflowQueryReply(v, err, value)
		}

	// WorkflowSetQueryHandlerReply
	case internal.WorkflowSetQueryHandlerReply:
		if v, ok := reply.(*messages.WorkflowSetQueryHandlerReply); ok {
			buildWorkflowSetQueryHandlerReply(v, err)
		}

	// WorkflowSetCacheSizeReply
	case internal.WorkflowSetCacheSizeReply:
		if v, ok := reply.(*messages.WorkflowSetCacheSizeReply); ok {
			buildWorkflowSetCacheSizeReply(v, err)
		}

	// WorkflowMutableReply
	case internal.WorkflowMutableReply:
		if v, ok := reply.(*messages.WorkflowMutableReply); ok {
			buildWorkflowMutableReply(v, err, value)
		}

	// WorkflowTerminateReply
	case internal.WorkflowTerminateReply:
		if v, ok := reply.(*messages.WorkflowTerminateReply); ok {
			buildWorkflowTerminateReply(v, err)
		}

	// WorkflowDescribeExecutionReply
	case internal.WorkflowDescribeExecutionReply:
		if v, ok := reply.(*messages.WorkflowDescribeExecutionReply); ok {
			buildWorkflowDescribeExecutionReply(v, err, value)
		}

	// WorkflowGetResultReply
	case internal.WorkflowGetResultReply:
		if v, ok := reply.(*messages.WorkflowGetResultReply); ok {
			buildWorkflowGetResultReply(v, err, value)
		}

	// WorkflowHasLastResultReply
	case internal.WorkflowHasLastResultReply:
		if v, ok := reply.(*messages.WorkflowHasLastResultReply); ok {
			buildWorkflowHasLastResultReply(v, err, value)
		}

	// WorkflowGetLastResultReply
	case internal.WorkflowGetLastResultReply:
		if v, ok := reply.(*messages.WorkflowGetLastResultReply); ok {
			buildWorkflowGetLastResultReply(v, err, value)
		}

	// WorkflowDisconnectContextReply
	case internal.WorkflowDisconnectContextReply:
		if v, ok := reply.(*messages.WorkflowDisconnectContextReply); ok {
			buildWorkflowDisconnectContextReply(v, err)
		}

	// WorkflowGetTimeReply
	case internal.WorkflowGetTimeReply:
		if v, ok := reply.(*messages.WorkflowGetTimeReply); ok {
			buildWorkflowGetTimeReply(v, err, value)
		}

	// WorkflowSleepReply
	case internal.WorkflowSleepReply:
		if v, ok := reply.(*messages.WorkflowSleepReply); ok {
			buildWorkflowSleepReply(v, err)
		}

	// WorkflowExecuteChildReply
	case internal.WorkflowExecuteChildReply:
		if v, ok := reply.(*messages.WorkflowExecuteChildReply); ok {
			buildWorkflowExecuteChildReply(v, err, value)
		}

	// WorkflowWaitForChildReply
	case internal.WorkflowWaitForChildReply:
		if v, ok := reply.(*messages.WorkflowWaitForChildReply); ok {
			buildWorkflowWaitForChildReply(v, err, value)
		}

	// WorkflowSignalChildReply
	case internal.WorkflowSignalChildReply:
		if v, ok := reply.(*messages.WorkflowSignalChildReply); ok {
			buildWorkflowSignalChildReply(v, err, value)
		}

	// WorkflowCancelChildReply
	case internal.WorkflowCancelChildReply:
		if v, ok := reply.(*messages.WorkflowCancelChildReply); ok {
			buildWorkflowCancelChildReply(v, err)
		}

	// WorkflowSignalReply
	case internal.WorkflowSignalReply:
		if v, ok := reply.(*messages.WorkflowSignalReply); ok {
			buildWorkflowSignalReply(v, err)
		}

	// WorkflowSignalSubscribeReply
	case internal.WorkflowSignalSubscribeReply:
		if v, ok := reply.(*messages.WorkflowSignalSubscribeReply); ok {
			buildWorkflowSignalSubscribeReply(v, err)
		}

	// WorkflowGetVersionReply
	case internal.WorkflowGetVersionReply:
		if v, ok := reply.(*messages.WorkflowGetVersionReply); ok {
			buildWorkflowGetVersionReply(v, err, value)
		}

	// WorkflowQueueNewReply
	case internal.WorkflowQueueNewReply:
		if v, ok := reply.(*messages.WorkflowQueueNewReply); ok {
			buildWorkflowQueueNewReply(v, err)
		}

	// WorkflowQueueWriteReply
	case internal.WorkflowQueueWriteReply:
		if v, ok := reply.(*messages.WorkflowQueueWriteReply); ok {
			buildWorkflowQueueWriteReply(v, err, value)
		}

	// WorkflowQueueReadReply
	case internal.WorkflowQueueReadReply:
		if v, ok := reply.(*messages.WorkflowQueueReadReply); ok {
			buildWorkflowQueueReadReply(v, err, value)
		}

	// WorkflowQueueCloseReply
	case internal.WorkflowQueueCloseReply:
		if v, ok := reply.(*messages.WorkflowQueueCloseReply); ok {
			buildWorkflowQueueCloseReply(v, err)
		}

	// -------------------------------------------------------------------------
	// activity message types

	// ActivityRegisterReply
	case internal.ActivityRegisterReply:
		if v, ok := reply.(*messages.ActivityRegisterReply); ok {
			buildActivityRegisterReply(v, err)
		}

	// ActivityExecuteReply
	case internal.ActivityExecuteReply:
		if v, ok := reply.(*messages.ActivityExecuteReply); ok {
			buildActivityExecuteReply(v, err, value)
		}

	// ActivityHasHeartbeatDetailsReply
	case internal.ActivityHasHeartbeatDetailsReply:
		if v, ok := reply.(*messages.ActivityHasHeartbeatDetailsReply); ok {
			buildActivityHasHeartbeatDetailsReply(v, err, value)
		}

	// ActivityGetHeartbeatDetailsReply
	case internal.ActivityGetHeartbeatDetailsReply:
		if v, ok := reply.(*messages.ActivityGetHeartbeatDetailsReply); ok {
			buildActivityGetHeartbeatDetailsReply(v, err, value)
		}

	// ActivityRecordHeartbeatReply
	case internal.ActivityRecordHeartbeatReply:
		if v, ok := reply.(*messages.ActivityRecordHeartbeatReply); ok {
			buildActivityRecordHeartbeatReply(v, err, value)
		}

	// ActivityGetInfoReply
	case internal.ActivityGetInfoReply:
		if v, ok := reply.(*messages.ActivityGetInfoReply); ok {
			buildActivityGetInfoReply(v, err, value)
		}

	// ActivityCompleteReply
	case internal.ActivityCompleteReply:
		if v, ok := reply.(*messages.ActivityCompleteReply); ok {
			buildActivityCompleteReply(v, err)
		}

	// ActivityExecuteLocalReply
	case internal.ActivityExecuteLocalReply:
		if v, ok := reply.(*messages.ActivityExecuteLocalReply); ok {
			buildActivityExecuteLocalReply(v, err, value)
		}

	// ActivityStartReply
	case internal.ActivityStartReply:
		if v, ok := reply.(*messages.ActivityStartReply); ok {
			buildActivityStartReply(v, err)
		}

	// ActivityGetResultReply
	case internal.ActivityGetResultReply:
		if v, ok := reply.(*messages.ActivityGetResultReply); ok {
			buildActivityGetResultReply(v, err, value)
		}

	// ActivityStartLocalReply
	case internal.ActivityStartLocalReply:
		if v, ok := reply.(*messages.ActivityStartLocalReply); ok {
			buildActivityStartLocalReply(v, err)
		}

	// ActivityGetLocalResultReply
	case internal.ActivityGetLocalResultReply:
		if v, ok := reply.(*messages.ActivityGetLocalResultReply); ok {
			buildActivityGetLocalResultReply(v, err, value)
		}

	// Undefined message type
	// This should never happen.
	default:
		err := fmt.Errorf("Error building reply for message type %s", reply.GetType())
		panic(err)
	}
}

func createReplyMessage(request messages.IProxyRequest) messages.IProxyReply {

	// get the correct reply type and initialize a new
	// reply corresponding to the request message type
	reply := messages.CreateNewTypedMessage(request.GetReplyType())
	if reflect.ValueOf(reply).IsNil() {
		return nil
	}

	reply.SetRequestID(request.GetRequestID())
	reply.SetClientID(request.GetClientID())

	if v, ok := reply.(messages.IProxyReply); ok {
		return v
	}

	return nil
}

// -------------------------------------------------------------------------
// Client message builders

func buildCancelReply(reply *messages.CancelReply, err error, wasCancelled ...interface{}) {
	reply.SetError(err)

	if len(wasCancelled) > 0 {
		if v, ok := wasCancelled[0].(bool); ok {
			reply.SetWasCancelled(v)
		}
	}
}

func buildConnectReply(reply *messages.ConnectReply, err error) {
	reply.SetError(err)
}

func buildDisconnectReply(reply *messages.DisconnectReply, err error) {
	reply.SetError(err)
}

func buildDomainDescribeReply(reply *messages.DomainDescribeReply, err error, describeDomainResponse ...interface{}) {
	reply.SetError(err)
	if len(describeDomainResponse) > 0 {
		if v, ok := describeDomainResponse[0].(*cadenceshared.DescribeDomainResponse); ok {
			reply.SetDomainInfoName(v.DomainInfo.Name)
			reply.SetDomainInfoDescription(v.DomainInfo.Description)
			reply.SetDomainInfoStatus(v.DomainInfo.Status)
			reply.SetConfigurationEmitMetrics(*v.Configuration.EmitMetric)
			reply.SetConfigurationRetentionDays(*v.Configuration.WorkflowExecutionRetentionPeriodInDays)
			reply.SetDomainInfoOwnerEmail(v.DomainInfo.OwnerEmail)
		}
	}
}

func buildDomainRegisterReply(reply *messages.DomainRegisterReply, err error) {
	reply.SetError(err)
}

func buildDomainUpdateReply(reply *messages.DomainUpdateReply, err error) {
	reply.SetError(err)
}

func buildDomainListReply(reply *messages.DomainListReply, err error, listDomainsResponse ...interface{}) {
	reply.SetError(err)
	if len(listDomainsResponse) > 0 {
		if v, ok := listDomainsResponse[0].(*cadenceshared.ListDomainsResponse); ok {
			reply.SetDomains(v.Domains)
			reply.SetNextPageToken(v.NextPageToken)
		}
	}
}

func buildHeartbeatReply(reply *messages.HeartbeatReply, err error) {
	reply.SetError(err)
}

func buildInitializeReply(reply *messages.InitializeReply, err error) {
	reply.SetError(err)
}

func buildTerminateReply(reply *messages.TerminateReply, err error) {
	reply.SetError(err)
}

func buildNewWorkerReply(reply *messages.NewWorkerReply, err error, workerID ...interface{}) {
	reply.SetError(err)
	if len(workerID) > 0 {
		if v, ok := workerID[0].(int64); ok {
			reply.SetWorkerID(v)
		}
	}
}

func buildStopWorkerReply(reply *messages.StopWorkerReply, err error) {
	reply.SetError(err)
}

func buildPingReply(reply *messages.PingReply, err error) {
	reply.SetError(err)
}

func buildDescribeTaskListReply(reply *messages.DescribeTaskListReply, err error, response ...interface{}) {
	reply.SetError(err)
	if len(response) > 0 {
		if v, ok := response[0].(*cadenceshared.DescribeTaskListResponse); ok {
			reply.SetResult(v)
		}
	}
}

// -------------------------------------------------------------------------
// Workflow message builders

func buildWorkflowRegisterReply(reply *messages.WorkflowRegisterReply, err error) {
	reply.SetError(err)
}

func buildWorkflowExecuteReply(reply *messages.WorkflowExecuteReply, err error, execution ...interface{}) {
	reply.SetError(err)
	if len(execution) > 0 {
		if v, ok := execution[0].(*workflow.Execution); ok {
			reply.SetExecution(v)
		}
	}
}

func buildWorkflowCancelReply(reply *messages.WorkflowCancelReply, err error) {
	reply.SetError(err)
}

func buildWorkflowTerminateReply(reply *messages.WorkflowTerminateReply, err error) {
	reply.SetError(err)
}

func buildWorkflowSignalSubscribeReply(reply *messages.WorkflowSignalSubscribeReply, err error) {
	reply.SetError(err)
}

func buildWorkflowSignalReply(reply *messages.WorkflowSignalReply, err error) {
	reply.SetError(err)
}

func buildWorkflowSignalWithStartReply(reply *messages.WorkflowSignalWithStartReply, err error, execution ...interface{}) {
	reply.SetError(err)
	if len(execution) > 0 {
		if v, ok := execution[0].(*workflow.Execution); ok {
			reply.SetExecution(v)
		}
	}
}

func buildWorkflowSetCacheSizeReply(reply *messages.WorkflowSetCacheSizeReply, err error) {
	reply.SetError(err)
}

func buildWorkflowQueryReply(reply *messages.WorkflowQueryReply, err error, result ...interface{}) {
	reply.SetError(err)
	if len(result) > 0 {
		if v, ok := result[0].([]byte); ok {
			reply.SetResult(v)
		}
	}
}

func buildWorkflowMutableReply(reply *messages.WorkflowMutableReply, err error, result ...interface{}) {
	reply.SetError(err)
	if len(result) > 0 {
		if v, ok := result[0].([]byte); ok {
			reply.SetResult(v)
		}
	}
}

func buildWorkflowDescribeExecutionReply(reply *messages.WorkflowDescribeExecutionReply, err error, description ...interface{}) {
	reply.SetError(err)
	if len(description) > 0 {
		if v, ok := description[0].(*cadenceshared.DescribeWorkflowExecutionResponse); ok {
			reply.SetDetails(v)
		}
	}
}

func buildWorkflowGetResultReply(reply *messages.WorkflowGetResultReply, err error, result ...interface{}) {
	reply.SetError(err)
	if len(result) > 0 {
		if v, ok := result[0].([]byte); ok {
			reply.SetResult(v)
		}
	}
}

func buildWorkflowHasLastResultReply(reply *messages.WorkflowHasLastResultReply, err error, hasResult ...interface{}) {
	reply.SetError(err)
	if len(hasResult) > 0 {
		if v, ok := hasResult[0].(bool); ok {
			reply.SetHasResult(v)
		}
	}
}

func buildWorkflowGetLastResultReply(reply *messages.WorkflowGetLastResultReply, err error, result ...interface{}) {
	reply.SetError(err)
	if len(result) > 0 {
		if v, ok := result[0].([]byte); ok {
			reply.SetResult(v)
		}
	}
}

func buildWorkflowDisconnectContextReply(reply *messages.WorkflowDisconnectContextReply, err error) {
	reply.SetError(err)
}

func buildWorkflowGetTimeReply(reply *messages.WorkflowGetTimeReply, err error, t ...interface{}) {
	reply.SetError(err)
	if len(t) > 0 {
		if v, ok := t[0].(time.Time); ok {
			reply.SetTime(v)
		}
	}
}

func buildWorkflowSleepReply(reply *messages.WorkflowSleepReply, err error) {
	reply.SetError(err)
}

func buildWorkflowExecuteChildReply(reply *messages.WorkflowExecuteChildReply, err error, childInfo ...interface{}) {
	reply.SetError(err)
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

func buildWorkflowWaitForChildReply(reply *messages.WorkflowWaitForChildReply, err error, result ...interface{}) {
	reply.SetError(err)
	if len(result) > 0 {
		if v, ok := result[0].([]byte); ok {
			reply.SetResult(v)
		}
	}
}

func buildWorkflowSignalChildReply(reply *messages.WorkflowSignalChildReply, err error, result ...interface{}) {
	reply.SetError(err)
	if len(result) > 0 {
		if v, ok := result[0].([]byte); ok {
			reply.SetResult(v)
		}
	}
}

func buildWorkflowCancelChildReply(reply *messages.WorkflowCancelChildReply, err error) {
	reply.SetError(err)
}

func buildWorkflowGetVersionReply(reply *messages.WorkflowGetVersionReply, err error, result ...interface{}) {
	reply.SetError(err)
	if len(result) > 0 {
		if v, ok := result[0].(workflow.Version); ok {
			reply.SetVersion(int32(v))
		}
	}
}

func buildWorkflowSetQueryHandlerReply(reply *messages.WorkflowSetQueryHandlerReply, err error) {
	reply.SetError(err)
}

func buildWorkflowQueueNewReply(reply *messages.WorkflowQueueNewReply, err error) {
	reply.SetError(err)
}

func buildWorkflowQueueWriteReply(reply *messages.WorkflowQueueWriteReply, err error, isFull ...interface{}) {
	reply.SetError(err)
	if len(isFull) > 0 {
		if v, ok := isFull[0].(bool); ok {
			reply.SetIsFull(v)
		}
	}
}

func buildWorkflowQueueReadReply(reply *messages.WorkflowQueueReadReply, err error, values ...interface{}) {
	reply.SetError(err)
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

func buildWorkflowQueueCloseReply(reply *messages.WorkflowQueueCloseReply, err error) {
	reply.SetError(err)
}

// -------------------------------------------------------------------------
// Activity message builders

func buildActivityRegisterReply(reply *messages.ActivityRegisterReply, err error) {
	reply.SetError(err)
}

func buildActivityExecuteReply(reply *messages.ActivityExecuteReply, err error, result ...interface{}) {
	reply.SetError(err)
	if len(result) > 0 {
		if v, ok := result[0].([]byte); ok {
			reply.SetResult(v)
		}
	}
}

func buildActivityHasHeartbeatDetailsReply(reply *messages.ActivityHasHeartbeatDetailsReply, err error, hasDetails ...interface{}) {
	reply.SetError(err)
	if len(hasDetails) > 0 {
		if v, ok := hasDetails[0].(bool); ok {
			reply.SetHasDetails(v)
		}
	}
}

func buildActivityGetHeartbeatDetailsReply(reply *messages.ActivityGetHeartbeatDetailsReply, err error, details ...interface{}) {
	reply.SetError(err)
	if len(details) > 0 {
		if v, ok := details[0].([]byte); ok {
			reply.SetDetails(v)
		}
	}
}

func buildActivityRecordHeartbeatReply(reply *messages.ActivityRecordHeartbeatReply, err error, details ...interface{}) {
	reply.SetError(err)
	if len(details) > 0 {
		if v, ok := details[0].([]byte); ok {
			reply.SetDetails(v)
		}
	}
}

func buildActivityGetInfoReply(reply *messages.ActivityGetInfoReply, err error, info ...interface{}) {
	reply.SetError(err)
	if len(info) > 0 {
		if v, ok := info[0].(*activity.Info); ok {
			reply.SetInfo(v)
		}
	}
}

func buildActivityCompleteReply(reply *messages.ActivityCompleteReply, err error) {
	reply.SetError(err)
}

func buildActivityExecuteLocalReply(reply *messages.ActivityExecuteLocalReply, err error, result ...interface{}) {
	reply.SetError(err)
	if len(result) > 0 {
		if v, ok := result[0].([]byte); ok {
			reply.SetResult(v)
		}
	}
}

func buildActivityStartReply(reply *messages.ActivityStartReply, err error) {
	reply.SetError(err)
}

func buildActivityGetResultReply(reply *messages.ActivityGetResultReply, err error, result ...interface{}) {
	reply.SetError(err)
	if len(result) > 0 {
		if v, ok := result[0].([]byte); ok {
			reply.SetResult(v)
		}
	}
}

func buildActivityStartLocalReply(reply *messages.ActivityStartLocalReply, err error) {
	reply.SetError(err)
}

func buildActivityGetLocalResultReply(reply *messages.ActivityGetLocalResultReply, err error, result ...interface{}) {
	reply.SetError(err)
	if len(result) > 0 {
		if v, ok := result[0].([]byte); ok {
			reply.SetResult(v)
		}
	}
}
