package common

import (
	"context"
	"fmt"
	"time"

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
		Service               workflowserviceclient.Interface
		Logger                *zap.Logger
		Config                Configuration
		Builder               *WorkflowClientBuilder
		registerDomainRequest *s.RegisterDomainRequest
	}

	// Configuration for running samples.
	Configuration struct {
		domainName       string
		serviceName      string
		hostPort         string
		dispatcherConfig *yarpc.Config
		clientOptions    *client.Options
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
	logger.Info(fmt.Sprintf("Domain:%s, HostPort:%s, Service:%s", h.Config.domainName, h.Config.hostPort, h.Config.serviceName))

	// set helper logger, scope and cadence client builder
	h.Logger = logger
	h.Builder = NewBuilder(logger).
		SetHostPort(h.Config.hostPort).
		SetDomain(h.Config.domainName).
		SetServiceName(h.Config.serviceName).
		SetClientOptions(h.Config.clientOptions).
		SetDispatcher(h.Config.dispatcherConfig)

	// set service
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

	// Set RegisterDomainRequest
	if h.registerDomainRequest == nil {

		// set defaults
		description := fmt.Sprintf("default domain description for domain: %s", h.Config.domainName)
		wrd := int32(workflowRetentionDays)

		// set default name, description, and workflow retention (3)
		h.registerDomainRequest.Name = &h.Config.domainName
		h.registerDomainRequest.Description = &description
		h.registerDomainRequest.WorkflowExecutionRetentionPeriodInDays = &wrd
	}

	request := h.registerDomainRequest

	// check if the registration returned any errors other than
	// DomainAlreadyExistsError
	err = domainClient.Register(context.Background(), request)
	if err != nil {
		if _, ok := err.(*s.DomainAlreadyExistsError); !ok {
			panic(err)
		}
		logger.Info("Domain already registered.", zap.String("Domain", h.Config.domainName))
	} else {
		logger.Info("Domain succeesfully registered.", zap.String("Domain", h.Config.domainName))
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

// CreateScope takes a map of tags, a stats reporter, and a duration
// It creates a new tally root scope from the parameters and returns it
func CreateScope(tags map[string]string, reporter tally.StatsReporter, reportEvery time.Duration) tally.Scope {
	scope, _ := tally.NewRootScope(tally.ScopeOptions{
		Tags:     tags,
		Reporter: reporter,
	}, reportEvery)

	return scope
}

// CreateTestScope takes a map of tags and a string prefix and
// creates a new tally test scope from the parameters and returns it
func CreateTestScope(prefix string, tags map[string]string) tally.Scope {
	return tally.NewTestScope(prefix, tags)
}
