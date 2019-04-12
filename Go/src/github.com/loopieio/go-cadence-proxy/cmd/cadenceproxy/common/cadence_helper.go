package common

import (
	"context"
	"fmt"

	"go.uber.org/cadence/.gen/go/cadence/workflowserviceclient"
	s "go.uber.org/cadence/.gen/go/shared"
	"go.uber.org/cadence/worker"
	"go.uber.org/yarpc"
	"go.uber.org/zap"

	"github.com/uber-go/tally"
	"go.uber.org/cadence/client"
)

const (
	workflowRetentionDays = 3
)

type (
	// Helper for configuring cadence-client and stoping/starting workflows
	// and cadence workers
	Helper struct {
		Service workflowserviceclient.Interface
		Scope   tally.Scope
		Logger  *zap.Logger
		Config  Configuration
		Builder *WorkflowClientBuilder
	}

	// Configuration for running samples.
	Configuration struct {
		DomainName            string
		ServiceName           string
		HostName              string
		Port                  string
		RegisterDomainRequest *s.RegisterDomainRequest
		DispatcherConfig      *yarpc.Config
		ClientOptions         *client.Options
	}
)

var domainCreated bool

// SetupServiceConfig setup the config for the sample code run
func (h *Helper) SetupServiceConfig() {
	if h.Service != nil {
		return
	}

	// Initialize logger for running samples
	logger, err := zap.NewDevelopment()
	if err != nil {
		panic(err)
	}
	logger.Info("Logger created.")
	logger.Info(fmt.Sprintf("Domain:%s, HostPort:%s, Port:%s, Service:%s", h.Config.DomainName, h.Config.HostName, h.Config.Port, h.Config.ServiceName))

	// set helper logger, scope and cadence client builder
	h.Logger = logger
	h.Scope = tally.NoopScope
	h.Builder = NewBuilder(logger).
		SetHostPort(fmt.Sprintf("%s:%s", h.Config.HostName, h.Config.Port)).
		SetDomain(h.Config.DomainName).
		SetMetricsScope(h.Scope)
	service, err := h.Builder.BuildServiceClient()
	if err != nil {
		panic(err)
	}
	h.Service = service

	if domainCreated {
		return
	}

	// create the domain client and register the domain
	domainClient, _ := h.Builder.BuildCadenceDomainClient()
	rDROpts := h.Config.RegisterDomainRequest
	request := &s.RegisterDomainRequest{
		Name:                                   &h.Config.DomainName,
		Description:                            rDROpts.Description,
		WorkflowExecutionRetentionPeriodInDays: rDROpts.WorkflowExecutionRetentionPeriodInDays,
		OwnerEmail:                             rDROpts.OwnerEmail,
		EmitMetric:                             rDROpts.EmitMetric,
		Clusters:                               rDROpts.Clusters,
		ActiveClusterName:                      rDROpts.ActiveClusterName,
		ArchivalBucketName:                     rDROpts.ArchivalBucketName,
		ArchivalStatus:                         rDROpts.ArchivalStatus,
		Data:                                   rDROpts.Data,
		SecurityToken:                          rDROpts.SecurityToken,
	}

	// check if the registration returned any errors other than
	// DomainAlreadyExistsError
	err = domainClient.Register(context.Background(), request)
	if err != nil {
		if _, ok := err.(*s.DomainAlreadyExistsError); !ok {
			panic(err)
		}
		logger.Info("Domain already registered.", zap.String("Domain", h.Config.DomainName))
	} else {
		logger.Info("Domain succeesfully registered.", zap.String("Domain", h.Config.DomainName))
	}
	domainCreated = true
}

// StartWorkflow starts a workflow
func (h *Helper) StartWorkflow(options client.StartWorkflowOptions, workflow interface{}, args ...interface{}) error {
	workflowClient, err := h.Builder.BuildCadenceClient()
	if err != nil {
		h.Logger.Error("Failed to build cadence client.", zap.Error(err))
		return err
	}

	we, err := workflowClient.StartWorkflow(context.Background(), options, workflow, args...)
	if err != nil {
		h.Logger.Error("Failed to create workflow", zap.Error(err))
		return err

	}

	h.Logger.Info("Started Workflow", zap.String("WorkflowID", we.ID), zap.String("RunID", we.RunID))
	return nil
}

// StartWorkers starts workflow worker and activity worker based on configured options.
func (h *Helper) StartWorkers(domainName, groupName string, options worker.Options) {
	worker := worker.New(h.Service, domainName, groupName, options)
	err := worker.Start()
	if err != nil {
		h.Logger.Error("Failed to start workers.", zap.Error(err))
		panic("Failed to start workers")
	}
}
