package endpoints

import (
	domain "github.com/loopieio/cadence-proxy/internal/cadence/cadencedomain"
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	"github.com/loopieio/cadence-proxy/internal/messages"

	cadenceshared "go.uber.org/cadence/.gen/go/shared"
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

func buildWorkflowExecuteReply(reply *messages.WorkflowExecuteReply, cadenceError *cadenceerrors.CadenceError) {
	reply.SetError(cadenceError)
}

func buildWorkflowInvokeReply(reply *messages.WorkflowInvokeReply, cadenceError *cadenceerrors.CadenceError) {
	reply.SetError(cadenceError)
}

func buildNewWorkerReply(reply *messages.NewWorkerReply, cadenceError *cadenceerrors.CadenceError) {
	reply.SetError(cadenceError)
}

func buildStopWorkerReply(reply *messages.StopWorkerReply, cadenceError *cadenceerrors.CadenceError) {
	reply.SetError(cadenceError)
}
