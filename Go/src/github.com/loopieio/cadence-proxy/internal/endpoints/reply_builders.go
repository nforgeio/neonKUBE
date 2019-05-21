package endpoints

import (
	domain "github.com/loopieio/cadence-proxy/internal/cadence/cadencedomains"
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	"github.com/loopieio/cadence-proxy/internal/messages"

	cadenceshared "go.uber.org/cadence/.gen/go/shared"
	"go.uber.org/cadence/workflow"
)

// -------------------------------------------------------------------------
// ProxyReply builders

func buildCancelReply(reply *messages.CancelReply, cadenceError *cadenceerrors.CadenceError, wasCancelled ...bool) {
	reply.SetError(cadenceError)

	if len(wasCancelled) > 0 {
		reply.SetWasCancelled(wasCancelled[0])
	}
}

func buildConnectReply(reply *messages.ConnectReply, cadenceError *cadenceerrors.CadenceError) {
	reply.SetError(cadenceError)
}

func buildDomainDescribeReply(reply *messages.DomainDescribeReply, cadenceError *cadenceerrors.CadenceError, describeDomainResponse ...*cadenceshared.DescribeDomainResponse) {
	reply.SetError(cadenceError)

	if len(describeDomainResponse) > 0 {
		d := describeDomainResponse[0]
		reply.SetDomainInfoName(d.DomainInfo.Name)
		reply.SetDomainInfoDescription(d.DomainInfo.Description)

		domainStatus := domain.DomainStatus(int(*d.DomainInfo.Status))
		reply.SetDomainInfoStatus(&domainStatus)
		reply.SetConfigurationEmitMetrics(*d.Configuration.EmitMetric)
		reply.SetConfigurationRetentionDays(*d.Configuration.WorkflowExecutionRetentionPeriodInDays)
		reply.SetDomainInfoOwnerEmail(d.DomainInfo.OwnerEmail)
	}
}

func buildDomainRegisterReply(reply *messages.DomainRegisterReply, cadenceError *cadenceerrors.CadenceError) {
	reply.SetError(cadenceError)
}

func buildDomainUpdateReply(reply *messages.DomainUpdateReply, cadenceError *cadenceerrors.CadenceError) {
	reply.SetError(cadenceError)
}

func buildHeartbeatReply(reply *messages.HeartbeatReply, cadenceError *cadenceerrors.CadenceError) {
	reply.SetError(cadenceError)
}

func buildInitializeReply(reply *messages.InitializeReply, cadenceError *cadenceerrors.CadenceError) {
	reply.SetError(cadenceError)
}

func buildTerminateReply(reply *messages.TerminateReply, cadenceError *cadenceerrors.CadenceError) {
	reply.SetError(cadenceError)
}

func buildWorkflowRegisterReply(reply *messages.WorkflowRegisterReply, cadenceError *cadenceerrors.CadenceError) {
	reply.SetError(cadenceError)
}

func buildWorkflowExecuteReply(reply *messages.WorkflowExecuteReply, cadenceError *cadenceerrors.CadenceError, execution ...*workflow.Execution) {
	reply.SetError(cadenceError)

	if len(execution) > 0 {
		reply.SetExecution(execution[0])
	}
}

func buildWorkflowInvokeReply(reply *messages.WorkflowInvokeReply, cadenceError *cadenceerrors.CadenceError, result ...[]byte) {
	reply.SetError(cadenceError)

	if len(result[0]) > 0 {
		reply.SetResult(result[0])
	}
}

func buildNewWorkerReply(reply *messages.NewWorkerReply, cadenceError *cadenceerrors.CadenceError, workerID ...int64) {
	reply.SetError(cadenceError)

	if len(workerID) > 0 {
		reply.SetWorkerID(workerID[0])
	}
}

func buildStopWorkerReply(reply *messages.StopWorkerReply, cadenceError *cadenceerrors.CadenceError) {
	reply.SetError(cadenceError)
}

func buildPingReply(reply *messages.PingReply, cadenceError *cadenceerrors.CadenceError) {
	reply.SetError(cadenceError)
}

func buildWorkflowCancelReply(reply *messages.WorkflowCancelReply, cadenceError *cadenceerrors.CadenceError) {
	reply.SetError(cadenceError)
}

func buildWorkflowTerminateReply(reply *messages.WorkflowTerminateReply, cadenceError *cadenceerrors.CadenceError) {
	reply.SetError(cadenceError)
}

func buildWorkflowSignalReply(reply *messages.WorkflowSignalReply, cadenceError *cadenceerrors.CadenceError) {
	reply.SetError(cadenceError)
}

func buildWorkflowSignalWithStartReply(reply *messages.WorkflowSignalWithStartReply, cadenceError *cadenceerrors.CadenceError, execution ...*workflow.Execution) {
	reply.SetError(cadenceError)

	if len(execution) > 0 {
		reply.SetExecution(execution[0])
	}
}

func buildWorkflowSetCacheSizeReply(reply *messages.WorkflowSetCacheSizeReply, cadenceError *cadenceerrors.CadenceError) {
	reply.SetError(cadenceError)
}

func buildWorkflowQueryReply(reply *messages.WorkflowQueryReply, cadenceError *cadenceerrors.CadenceError, result ...[]byte) {
	reply.SetError(cadenceError)

	if len(result) > 0 {
		reply.SetResult(result[0])
	}
}
