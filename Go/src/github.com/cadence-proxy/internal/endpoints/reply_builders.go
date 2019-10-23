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
	proxyclient "github.com/cadence-proxy/internal/cadence/client"
	proxyerror "github.com/cadence-proxy/internal/cadence/error"
	"github.com/cadence-proxy/internal/messages"
)

func buildReply(reply messages.IProxyReply, cadenceError *proxyerror.CadenceError, values ...interface{}) {

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
			buildInitializeReply(v, cadenceError)
		}

	// HeartbeatReply
	case internal.HeartbeatReply:
		if v, ok := reply.(*messages.HeartbeatReply); ok {
			buildHeartbeatReply(v, cadenceError)
		}

	// CancelReply
	case internal.CancelReply:
		if v, ok := reply.(*messages.CancelReply); ok {
			buildCancelReply(v, cadenceError, value)
		}

	// ConnectReply
	case internal.ConnectReply:
		if v, ok := reply.(*messages.ConnectReply); ok {
			buildConnectReply(v, cadenceError)
		}

	// ConnectReply
	case internal.DisconnectReply:
		if v, ok := reply.(*messages.DisconnectReply); ok {
			buildDisconnectReply(v, cadenceError)
		}

	// DomainDescribeReply
	case internal.DomainDescribeReply:
		if v, ok := reply.(*messages.DomainDescribeReply); ok {
			buildDomainDescribeReply(v, cadenceError, value)
		}

	// DomainRegisterReply
	case internal.DomainRegisterReply:
		if v, ok := reply.(*messages.DomainRegisterReply); ok {
			buildDomainRegisterReply(v, cadenceError)
		}

	// DomainUpdateReply
	case internal.DomainUpdateReply:
		if v, ok := reply.(*messages.DomainUpdateReply); ok {
			buildDomainUpdateReply(v, cadenceError)
		}

	// TerminateReply
	case internal.TerminateReply:
		if v, ok := reply.(*messages.TerminateReply); ok {
			buildTerminateReply(v, cadenceError)
		}

	// NewWorkerReply
	case internal.NewWorkerReply:
		if v, ok := reply.(*messages.NewWorkerReply); ok {
			buildNewWorkerReply(v, cadenceError, value)
		}

	// StopWorkerReply
	case internal.StopWorkerReply:
		if v, ok := reply.(*messages.StopWorkerReply); ok {
			buildStopWorkerReply(v, cadenceError)
		}

	// PingReply
	case internal.PingReply:
		if v, ok := reply.(*messages.PingReply); ok {
			buildPingReply(v, cadenceError)
		}

	// -------------------------------------------------------------------------
	// workflow message types

	// WorkflowExecuteReply
	case internal.WorkflowExecuteReply:
		if v, ok := reply.(*messages.WorkflowExecuteReply); ok {
			buildWorkflowExecuteReply(v, cadenceError, value)
		}

	// WorkflowRegisterReply
	case internal.WorkflowRegisterReply:
		if v, ok := reply.(*messages.WorkflowRegisterReply); ok {
			buildWorkflowRegisterReply(v, cadenceError)
		}

	// WorkflowCancelReply
	case internal.WorkflowCancelReply:
		if v, ok := reply.(*messages.WorkflowCancelReply); ok {
			buildWorkflowCancelReply(v, cadenceError)
		}

	// WorkflowSignalWithStartReply
	case internal.WorkflowSignalWithStartReply:
		if v, ok := reply.(*messages.WorkflowSignalWithStartReply); ok {
			buildWorkflowSignalWithStartReply(v, cadenceError, value)
		}

	// WorkflowQueryReply
	case internal.WorkflowQueryReply:
		if v, ok := reply.(*messages.WorkflowQueryReply); ok {
			buildWorkflowQueryReply(v, cadenceError, value)
		}

	// WorkflowSetQueryHandlerReply
	case internal.WorkflowSetQueryHandlerReply:
		if v, ok := reply.(*messages.WorkflowSetQueryHandlerReply); ok {
			buildWorkflowSetQueryHandlerReply(v, cadenceError)
		}

	// WorkflowSetCacheSizeReply
	case internal.WorkflowSetCacheSizeReply:
		if v, ok := reply.(*messages.WorkflowSetCacheSizeReply); ok {
			buildWorkflowSetCacheSizeReply(v, cadenceError)
		}

	// WorkflowMutableReply
	case internal.WorkflowMutableReply:
		if v, ok := reply.(*messages.WorkflowMutableReply); ok {
			buildWorkflowMutableReply(v, cadenceError, value)
		}

	// WorkflowTerminateReply
	case internal.WorkflowTerminateReply:
		if v, ok := reply.(*messages.WorkflowTerminateReply); ok {
			buildWorkflowTerminateReply(v, cadenceError)
		}

	// WorkflowDescribeExecutionReply
	case internal.WorkflowDescribeExecutionReply:
		if v, ok := reply.(*messages.WorkflowDescribeExecutionReply); ok {
			buildWorkflowDescribeExecutionReply(v, cadenceError, value)
		}

	// WorkflowGetResultReply
	case internal.WorkflowGetResultReply:
		if v, ok := reply.(*messages.WorkflowGetResultReply); ok {
			buildWorkflowGetResultReply(v, cadenceError, value)
		}

	// WorkflowHasLastResultReply
	case internal.WorkflowHasLastResultReply:
		if v, ok := reply.(*messages.WorkflowHasLastResultReply); ok {
			buildWorkflowHasLastResultReply(v, cadenceError, value)
		}

	// WorkflowGetLastResultReply
	case internal.WorkflowGetLastResultReply:
		if v, ok := reply.(*messages.WorkflowGetLastResultReply); ok {
			buildWorkflowGetLastResultReply(v, cadenceError, value)
		}

	// WorkflowDisconnectContextReply
	case internal.WorkflowDisconnectContextReply:
		if v, ok := reply.(*messages.WorkflowDisconnectContextReply); ok {
			buildWorkflowDisconnectContextReply(v, cadenceError)
		}

	// WorkflowGetTimeReply
	case internal.WorkflowGetTimeReply:
		if v, ok := reply.(*messages.WorkflowGetTimeReply); ok {
			buildWorkflowGetTimeReply(v, cadenceError, value)
		}

	// WorkflowSleepReply
	case internal.WorkflowSleepReply:
		if v, ok := reply.(*messages.WorkflowSleepReply); ok {
			buildWorkflowSleepReply(v, cadenceError)
		}

	// WorkflowExecuteChildReply
	case internal.WorkflowExecuteChildReply:
		if v, ok := reply.(*messages.WorkflowExecuteChildReply); ok {
			buildWorkflowExecuteChildReply(v, cadenceError, value)
		}

	// WorkflowWaitForChildReply
	case internal.WorkflowWaitForChildReply:
		if v, ok := reply.(*messages.WorkflowWaitForChildReply); ok {
			buildWorkflowWaitForChildReply(v, cadenceError, value)
		}

	// WorkflowSignalChildReply
	case internal.WorkflowSignalChildReply:
		if v, ok := reply.(*messages.WorkflowSignalChildReply); ok {
			buildWorkflowSignalChildReply(v, cadenceError, value)
		}

	// WorkflowCancelChildReply
	case internal.WorkflowCancelChildReply:
		if v, ok := reply.(*messages.WorkflowCancelChildReply); ok {
			buildWorkflowCancelChildReply(v, cadenceError)
		}

	// WorkflowSignalReply
	case internal.WorkflowSignalReply:
		if v, ok := reply.(*messages.WorkflowSignalReply); ok {
			buildWorkflowSignalReply(v, cadenceError)
		}

	// WorkflowSignalSubscribeReply
	case internal.WorkflowSignalSubscribeReply:
		if v, ok := reply.(*messages.WorkflowSignalSubscribeReply); ok {
			buildWorkflowSignalSubscribeReply(v, cadenceError)
		}

	// WorkflowGetVersionReply
	case internal.WorkflowGetVersionReply:
		if v, ok := reply.(*messages.WorkflowGetVersionReply); ok {
			buildWorkflowGetVersionReply(v, cadenceError, value)
		}

	// WorkflowQueueNewReply
	case internal.WorkflowQueueNewReply:
		if v, ok := reply.(*messages.WorkflowQueueNewReply); ok {
			buildWorkflowQueueNewReply(v, cadenceError)
		}

	// WorkflowQueueWriteReply
	case internal.WorkflowQueueWriteReply:
		if v, ok := reply.(*messages.WorkflowQueueWriteReply); ok {
			buildWorkflowQueueWriteReply(v, cadenceError, value)
		}

	// WorkflowQueueReadReply
	case internal.WorkflowQueueReadReply:
		if v, ok := reply.(*messages.WorkflowQueueReadReply); ok {
			buildWorkflowQueueReadReply(v, cadenceError, value)
		}

	// WorkflowQueueLengthReply
	case internal.WorkflowQueueLengthReply:
		if v, ok := reply.(*messages.WorkflowQueueLengthReply); ok {
			buildWorkflowQueueLengthReply(v, cadenceError, value)
		}

	// WorkflowQueueCloseReply
	case internal.WorkflowQueueCloseReply:
		if v, ok := reply.(*messages.WorkflowQueueCloseReply); ok {
			buildWorkflowQueueCloseReply(v, cadenceError)
		}

	// -------------------------------------------------------------------------
	// activity message types

	// ActivityRegisterReply
	case internal.ActivityRegisterReply:
		if v, ok := reply.(*messages.ActivityRegisterReply); ok {
			buildActivityRegisterReply(v, cadenceError)
		}

	// ActivityExecuteReply
	case internal.ActivityExecuteReply:
		if v, ok := reply.(*messages.ActivityExecuteReply); ok {
			buildActivityExecuteReply(v, cadenceError, value)
		}

	// ActivityHasHeartbeatDetailsReply
	case internal.ActivityHasHeartbeatDetailsReply:
		if v, ok := reply.(*messages.ActivityHasHeartbeatDetailsReply); ok {
			buildActivityHasHeartbeatDetailsReply(v, cadenceError, value)
		}

	// ActivityGetHeartbeatDetailsReply
	case internal.ActivityGetHeartbeatDetailsReply:
		if v, ok := reply.(*messages.ActivityGetHeartbeatDetailsReply); ok {
			buildActivityGetHeartbeatDetailsReply(v, cadenceError, value)
		}

	// ActivityRecordHeartbeatReply
	case internal.ActivityRecordHeartbeatReply:
		if v, ok := reply.(*messages.ActivityRecordHeartbeatReply); ok {
			buildActivityRecordHeartbeatReply(v, cadenceError, value)
		}

	// ActivityGetInfoReply
	case internal.ActivityGetInfoReply:
		if v, ok := reply.(*messages.ActivityGetInfoReply); ok {
			buildActivityGetInfoReply(v, cadenceError, value)
		}

	// ActivityCompleteReply
	case internal.ActivityCompleteReply:
		if v, ok := reply.(*messages.ActivityCompleteReply); ok {
			buildActivityCompleteReply(v, cadenceError)
		}

	// ActivityExecuteLocalReply
	case internal.ActivityExecuteLocalReply:
		if v, ok := reply.(*messages.ActivityExecuteLocalReply); ok {
			buildActivityExecuteLocalReply(v, cadenceError, value)
		}

	// ActivityStartReply
	case internal.ActivityStartReply:
		if v, ok := reply.(*messages.ActivityStartReply); ok {
			buildActivityStartReply(v, cadenceError)
		}

	// ActivityGetResultReply
	case internal.ActivityGetResultReply:
		if v, ok := reply.(*messages.ActivityGetResultReply); ok {
			buildActivityGetResultReply(v, cadenceError, value)
		}

	// ActivityStartLocalReply
	case internal.ActivityStartLocalReply:
		if v, ok := reply.(*messages.ActivityStartLocalReply); ok {
			buildActivityStartLocalReply(v, cadenceError)
		}

	// ActivityGetLocalResultReply
	case internal.ActivityGetLocalResultReply:
		if v, ok := reply.(*messages.ActivityGetLocalResultReply); ok {
			buildActivityGetLocalResultReply(v, cadenceError, value)
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

func buildCancelReply(reply *messages.CancelReply, cadenceError *proxyerror.CadenceError, wasCancelled ...interface{}) {
	reply.SetError(cadenceError)

	if len(wasCancelled) > 0 {
		if v, ok := wasCancelled[0].(bool); ok {
			reply.SetWasCancelled(v)
		}
	}
}

func buildConnectReply(reply *messages.ConnectReply, cadenceError *proxyerror.CadenceError) {
	reply.SetError(cadenceError)
}

func buildDisconnectReply(reply *messages.DisconnectReply, cadenceError *proxyerror.CadenceError) {
	reply.SetError(cadenceError)
}

func buildDomainDescribeReply(reply *messages.DomainDescribeReply, cadenceError *proxyerror.CadenceError, describeDomainResponse ...interface{}) {
	reply.SetError(cadenceError)
	if len(describeDomainResponse) > 0 {
		if v, ok := describeDomainResponse[0].(*cadenceshared.DescribeDomainResponse); ok {
			reply.SetDomainInfoName(v.DomainInfo.Name)
			reply.SetDomainInfoDescription(v.DomainInfo.Description)
			reply.SetDomainInfoStatus(proxyclient.StringToDomainStatus(v.DomainInfo.Status.String()))
			reply.SetConfigurationEmitMetrics(*v.Configuration.EmitMetric)
			reply.SetConfigurationRetentionDays(*v.Configuration.WorkflowExecutionRetentionPeriodInDays)
			reply.SetDomainInfoOwnerEmail(v.DomainInfo.OwnerEmail)
		}
	}
}

func buildDomainRegisterReply(reply *messages.DomainRegisterReply, cadenceError *proxyerror.CadenceError) {
	reply.SetError(cadenceError)
}

func buildDomainUpdateReply(reply *messages.DomainUpdateReply, cadenceError *proxyerror.CadenceError) {
	reply.SetError(cadenceError)
}

func buildHeartbeatReply(reply *messages.HeartbeatReply, cadenceError *proxyerror.CadenceError) {
	reply.SetError(cadenceError)
}

func buildInitializeReply(reply *messages.InitializeReply, cadenceError *proxyerror.CadenceError) {
	reply.SetError(cadenceError)
}

func buildTerminateReply(reply *messages.TerminateReply, cadenceError *proxyerror.CadenceError) {
	reply.SetError(cadenceError)
}

func buildNewWorkerReply(reply *messages.NewWorkerReply, cadenceError *proxyerror.CadenceError, workerID ...interface{}) {
	reply.SetError(cadenceError)
	if len(workerID) > 0 {
		if v, ok := workerID[0].(int64); ok {
			reply.SetWorkerID(v)
		}
	}
}

func buildStopWorkerReply(reply *messages.StopWorkerReply, cadenceError *proxyerror.CadenceError) {
	reply.SetError(cadenceError)
}

func buildPingReply(reply *messages.PingReply, cadenceError *proxyerror.CadenceError) {
	reply.SetError(cadenceError)
}

// -------------------------------------------------------------------------
// Workflow message builders

func buildWorkflowRegisterReply(reply *messages.WorkflowRegisterReply, cadenceError *proxyerror.CadenceError) {
	reply.SetError(cadenceError)
}

func buildWorkflowExecuteReply(reply *messages.WorkflowExecuteReply, cadenceError *proxyerror.CadenceError, execution ...interface{}) {
	reply.SetError(cadenceError)
	if len(execution) > 0 {
		if v, ok := execution[0].(*workflow.Execution); ok {
			reply.SetExecution(v)
		}
	}
}

func buildWorkflowCancelReply(reply *messages.WorkflowCancelReply, cadenceError *proxyerror.CadenceError) {
	reply.SetError(cadenceError)
}

func buildWorkflowTerminateReply(reply *messages.WorkflowTerminateReply, cadenceError *proxyerror.CadenceError) {
	reply.SetError(cadenceError)
}

func buildWorkflowSignalSubscribeReply(reply *messages.WorkflowSignalSubscribeReply, cadenceError *proxyerror.CadenceError) {
	reply.SetError(cadenceError)
}

func buildWorkflowSignalReply(reply *messages.WorkflowSignalReply, cadenceError *proxyerror.CadenceError) {
	reply.SetError(cadenceError)
}

func buildWorkflowSignalWithStartReply(reply *messages.WorkflowSignalWithStartReply, cadenceError *proxyerror.CadenceError, execution ...interface{}) {
	reply.SetError(cadenceError)
	if len(execution) > 0 {
		if v, ok := execution[0].(*workflow.Execution); ok {
			reply.SetExecution(v)
		}
	}
}

func buildWorkflowSetCacheSizeReply(reply *messages.WorkflowSetCacheSizeReply, cadenceError *proxyerror.CadenceError) {
	reply.SetError(cadenceError)
}

func buildWorkflowQueryReply(reply *messages.WorkflowQueryReply, cadenceError *proxyerror.CadenceError, result ...interface{}) {
	reply.SetError(cadenceError)
	if len(result) > 0 {
		if v, ok := result[0].([]byte); ok {
			reply.SetResult(v)
		}
	}
}

func buildWorkflowMutableReply(reply *messages.WorkflowMutableReply, cadenceError *proxyerror.CadenceError, result ...interface{}) {
	reply.SetError(cadenceError)
	if len(result) > 0 {
		if v, ok := result[0].([]byte); ok {
			reply.SetResult(v)
		}
	}
}

func buildWorkflowDescribeExecutionReply(reply *messages.WorkflowDescribeExecutionReply, cadenceError *proxyerror.CadenceError, description ...interface{}) {
	reply.SetError(cadenceError)
	if len(description) > 0 {
		if v, ok := description[0].(*cadenceshared.DescribeWorkflowExecutionResponse); ok {
			reply.SetDetails(v)
		}
	}
}

func buildWorkflowGetResultReply(reply *messages.WorkflowGetResultReply, cadenceError *proxyerror.CadenceError, result ...interface{}) {
	reply.SetError(cadenceError)
	if len(result) > 0 {
		if v, ok := result[0].([]byte); ok {
			reply.SetResult(v)
		}
	}
}

func buildWorkflowHasLastResultReply(reply *messages.WorkflowHasLastResultReply, cadenceError *proxyerror.CadenceError, hasResult ...interface{}) {
	reply.SetError(cadenceError)
	if len(hasResult) > 0 {
		if v, ok := hasResult[0].(bool); ok {
			reply.SetHasResult(v)
		}
	}
}

func buildWorkflowGetLastResultReply(reply *messages.WorkflowGetLastResultReply, cadenceError *proxyerror.CadenceError, result ...interface{}) {
	reply.SetError(cadenceError)
	if len(result) > 0 {
		if v, ok := result[0].([]byte); ok {
			reply.SetResult(v)
		}
	}
}

func buildWorkflowDisconnectContextReply(reply *messages.WorkflowDisconnectContextReply, cadenceError *proxyerror.CadenceError) {
	reply.SetError(cadenceError)
}

func buildWorkflowGetTimeReply(reply *messages.WorkflowGetTimeReply, cadenceError *proxyerror.CadenceError, t ...interface{}) {
	reply.SetError(cadenceError)
	if len(t) > 0 {
		if v, ok := t[0].(time.Time); ok {
			reply.SetTime(v)
		}
	}
}

func buildWorkflowSleepReply(reply *messages.WorkflowSleepReply, cadenceError *proxyerror.CadenceError) {
	reply.SetError(cadenceError)
}

func buildWorkflowExecuteChildReply(reply *messages.WorkflowExecuteChildReply, cadenceError *proxyerror.CadenceError, childInfo ...interface{}) {
	reply.SetError(cadenceError)
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

func buildWorkflowWaitForChildReply(reply *messages.WorkflowWaitForChildReply, cadenceError *proxyerror.CadenceError, result ...interface{}) {
	reply.SetError(cadenceError)
	if len(result) > 0 {
		if v, ok := result[0].([]byte); ok {
			reply.SetResult(v)
		}
	}
}

func buildWorkflowSignalChildReply(reply *messages.WorkflowSignalChildReply, cadenceError *proxyerror.CadenceError, result ...interface{}) {
	reply.SetError(cadenceError)
	if len(result) > 0 {
		if v, ok := result[0].([]byte); ok {
			reply.SetResult(v)
		}
	}
}

func buildWorkflowCancelChildReply(reply *messages.WorkflowCancelChildReply, cadenceError *proxyerror.CadenceError) {
	reply.SetError(cadenceError)
}

func buildWorkflowGetVersionReply(reply *messages.WorkflowGetVersionReply, cadenceError *proxyerror.CadenceError, result ...interface{}) {
	reply.SetError(cadenceError)
	if len(result) > 0 {
		if v, ok := result[0].(workflow.Version); ok {
			reply.SetVersion(int32(v))
		}
	}
}

func buildWorkflowSetQueryHandlerReply(reply *messages.WorkflowSetQueryHandlerReply, cadenceError *proxyerror.CadenceError) {
	reply.SetError(cadenceError)
}

func buildWorkflowQueueNewReply(reply *messages.WorkflowQueueNewReply, cadenceError *proxyerror.CadenceError) {
	reply.SetError(cadenceError)
}

func buildWorkflowQueueWriteReply(reply *messages.WorkflowQueueWriteReply, cadenceError *proxyerror.CadenceError, isFull ...interface{}) {
	reply.SetError(cadenceError)
	if len(isFull) > 0 {
		if v, ok := isFull[0].(bool); ok {
			reply.SetIsFull(v)
		}
	}
}

func buildWorkflowQueueReadReply(reply *messages.WorkflowQueueReadReply, cadenceError *proxyerror.CadenceError, values ...interface{}) {
	reply.SetError(cadenceError)
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

func buildWorkflowQueueLengthReply(reply *messages.WorkflowQueueLengthReply, cadenceError *proxyerror.CadenceError, length ...interface{}) {
	reply.SetError(cadenceError)
	if len(length) > 0 {
		if v, ok := length[0].(int32); ok {
			reply.SetLength(v)
		}
	}
}

func buildWorkflowQueueCloseReply(reply *messages.WorkflowQueueCloseReply, cadenceError *proxyerror.CadenceError) {
	reply.SetError(cadenceError)
}

// -------------------------------------------------------------------------
// Activity message builders

func buildActivityRegisterReply(reply *messages.ActivityRegisterReply, cadenceError *proxyerror.CadenceError) {
	reply.SetError(cadenceError)
}

func buildActivityExecuteReply(reply *messages.ActivityExecuteReply, cadenceError *proxyerror.CadenceError, result ...interface{}) {
	reply.SetError(cadenceError)
	if len(result) > 0 {
		if v, ok := result[0].([]byte); ok {
			reply.SetResult(v)
		}
	}
}

func buildActivityHasHeartbeatDetailsReply(reply *messages.ActivityHasHeartbeatDetailsReply, cadenceError *proxyerror.CadenceError, hasDetails ...interface{}) {
	reply.SetError(cadenceError)
	if len(hasDetails) > 0 {
		if v, ok := hasDetails[0].(bool); ok {
			reply.SetHasDetails(v)
		}
	}
}

func buildActivityGetHeartbeatDetailsReply(reply *messages.ActivityGetHeartbeatDetailsReply, cadenceError *proxyerror.CadenceError, details ...interface{}) {
	reply.SetError(cadenceError)
	if len(details) > 0 {
		if v, ok := details[0].([]byte); ok {
			reply.SetDetails(v)
		}
	}
}

func buildActivityRecordHeartbeatReply(reply *messages.ActivityRecordHeartbeatReply, cadenceError *proxyerror.CadenceError, details ...interface{}) {
	reply.SetError(cadenceError)
	if len(details) > 0 {
		if v, ok := details[0].([]byte); ok {
			reply.SetDetails(v)
		}
	}
}

func buildActivityGetInfoReply(reply *messages.ActivityGetInfoReply, cadenceError *proxyerror.CadenceError, info ...interface{}) {
	reply.SetError(cadenceError)
	if len(info) > 0 {
		if v, ok := info[0].(*activity.Info); ok {
			reply.SetInfo(v)
		}
	}
}

func buildActivityCompleteReply(reply *messages.ActivityCompleteReply, cadenceError *proxyerror.CadenceError) {
	reply.SetError(cadenceError)
}

func buildActivityExecuteLocalReply(reply *messages.ActivityExecuteLocalReply, cadenceError *proxyerror.CadenceError, result ...interface{}) {
	reply.SetError(cadenceError)
	if len(result) > 0 {
		if v, ok := result[0].([]byte); ok {
			reply.SetResult(v)
		}
	}
}

func buildActivityStartReply(reply *messages.ActivityStartReply, cadenceError *proxyerror.CadenceError) {
	reply.SetError(cadenceError)
}

func buildActivityGetResultReply(reply *messages.ActivityGetResultReply, cadenceError *proxyerror.CadenceError, result ...interface{}) {
	reply.SetError(cadenceError)
	if len(result) > 0 {
		if v, ok := result[0].([]byte); ok {
			reply.SetResult(v)
		}
	}
}

func buildActivityStartLocalReply(reply *messages.ActivityStartLocalReply, cadenceError *proxyerror.CadenceError) {
	reply.SetError(cadenceError)
}

func buildActivityGetLocalResultReply(reply *messages.ActivityGetLocalResultReply, cadenceError *proxyerror.CadenceError, result ...interface{}) {
	reply.SetError(cadenceError)
	if len(result) > 0 {
		if v, ok := result[0].([]byte); ok {
			reply.SetResult(v)
		}
	}
}
