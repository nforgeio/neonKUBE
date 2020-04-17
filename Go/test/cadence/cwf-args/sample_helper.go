package main

import (
	"context"
	"fmt"
	"io/ioutil"
	"strconv"

	"go.uber.org/cadence/encoded"
	"go.uber.org/cadence/worker"
	"go.uber.org/cadence/workflow"
	"go.uber.org/zap"

	"github.com/uber-go/tally"
	"go.uber.org/cadence/.gen/go/cadence/workflowserviceclient"
	"go.uber.org/cadence/client"
	"gopkg.in/yaml.v2"
)

type (
	// SampleHelper class for workflow sample helper.
	SampleHelper struct {
		Service        workflowserviceclient.Interface
		Scope          tally.Scope
		Logger         *zap.Logger
		Config         Configuration
		Builder        *WorkflowClientBuilder
		DataConverter  encoded.DataConverter
		CtxPropagators []workflow.ContextPropagator
		Worker         worker.Worker
	}

	// Configuration for running samples.
	Configuration struct {
		DomainName      string `yaml:"domain"`
		ServiceName     string `yaml:"service"`
		HostNameAndPort string `yaml:"host"`
	}
)

// SetupServiceConfig setup the config for the sample code run
func (h *SampleHelper) SetupServiceConfig(configPath string) {
	if h.Service != nil {
		return
	}

	// Initialize developer config for running samples
	configData, err := ioutil.ReadFile(configPath)
	if err != nil {
		panic(fmt.Sprintf("Config file read failed: %v, Error: %v", configPath, err))
	}

	if err := yaml.Unmarshal(configData, &h.Config); err != nil {
		panic(fmt.Sprintf("Error initializing configuration: %v", err))
	}

	// Initialize logger for running samples
	logger, err := zap.NewDevelopment()
	if err != nil {
		panic(err)
	}

	logger.Info("Logger created.")
	h.Logger = logger
	h.Scope = tally.NoopScope
	h.Builder = NewBuilder(logger).
		SetHostPort(h.Config.HostNameAndPort).
		SetDomain(h.Config.DomainName).
		SetMetricsScope(h.Scope).
		SetDataConverter(h.DataConverter).
		SetContextPropagators(h.CtxPropagators)
	service, err := h.Builder.BuildServiceClient()
	if err != nil {
		panic(err)
	}
	h.Service = service

	domainClient, _ := h.Builder.BuildCadenceDomainClient()
	_, err = domainClient.Describe(context.Background(), h.Config.DomainName)
	if err != nil {
		logger.Info("Domain doesn't exist", zap.String("Domain", h.Config.DomainName), zap.Error(err))
	} else {
		logger.Info("Domain successfully registered.", zap.String("Domain", h.Config.DomainName))
	}
}

// Executes a workflow returning a string and waits for it to complete.
func (h *SampleHelper) ExecuteWorkflow(options client.StartWorkflowOptions, workflow interface{}, args ...interface{}) {
	workflowClient, err := h.Builder.BuildCadenceClient()
	if err != nil {
		h.Logger.Error("Failed to build cadence client.", zap.Error(err))
		panic(err)
	}

	ctx := context.Background()

	wr, err := workflowClient.ExecuteWorkflow(ctx, options, workflow, args...)
	if err != nil {
		h.Logger.Error("Failed to create workflow", zap.Error(err))
		panic("Failed to create workflow.")

	} else {
		h.Logger.Info("Started Workflow", zap.String("RunID", wr.GetRunID()))
	}

	var result string

	err = wr.Get(ctx, &result)
	if err != nil {
		h.Logger.Error("Failed to get result", zap.Error(err))
		panic("Failed to get result.")

	} else {

		h.Logger.Info("Result: " + result)
	}
}

// Executes a workflow returning an int array and waits for it to complete.
func (h *SampleHelper) ExecuteArrayWorkflow(options client.StartWorkflowOptions, workflow interface{}, args ...interface{}) {
	workflowClient, err := h.Builder.BuildCadenceClient()
	if err != nil {
		h.Logger.Error("Failed to build cadence client.", zap.Error(err))
		panic(err)
	}

	ctx := context.Background()

	wr, err := workflowClient.ExecuteWorkflow(ctx, options, workflow, args...)
	if err != nil {
		h.Logger.Error("Failed to create workflow", zap.Error(err))
		panic("Failed to create workflow.")

	} else {
		h.Logger.Info("Started Workflow", zap.String("RunID", wr.GetRunID()))
	}

	var result []int32

	err = wr.Get(ctx, &result)
	if err != nil {
		h.Logger.Error("Failed to get result", zap.Error(err))
		panic("Failed to get result.")

	} else {

		out := "["

		for i := 0; i < len(result); i++ {
			if i > 0 {
				out = out + ", "
			}

			out = out + strconv.Itoa(int(result[i]))
		}

		out = out + "]"

		h.Logger.Info("Result: " + out)
	}
}

// StartWorkflow starts a workflow
func (h *SampleHelper) StartWorkflow(options client.StartWorkflowOptions, workflow interface{}, args ...interface{}) {
	h.StartWorkflowWithCtx(context.Background(), options, workflow, args...)
}

// StartWorkflowWithCtx starts a workflow with the provided context
func (h *SampleHelper) StartWorkflowWithCtx(ctx context.Context, options client.StartWorkflowOptions, workflow interface{}, args ...interface{}) {
	workflowClient, err := h.Builder.BuildCadenceClient()
	if err != nil {
		h.Logger.Error("Failed to build cadence client.", zap.Error(err))
		panic(err)
	}

	we, err := workflowClient.StartWorkflow(ctx, options, workflow, args...)
	if err != nil {
		h.Logger.Error("Failed to create workflow", zap.Error(err))
		panic("Failed to create workflow.")

	} else {
		h.Logger.Info("Started Workflow", zap.String("WorkflowID", we.ID), zap.String("RunID", we.RunID))
	}
}

// SignalWithStartWorkflowWithCtx signals workflow and starts it if it's not yet started
func (h *SampleHelper) SignalWithStartWorkflowWithCtx(ctx context.Context, workflowID string, signalName string, signalArg interface{},
	options client.StartWorkflowOptions, workflow interface{}, workflowArgs ...interface{}) {
	workflowClient, err := h.Builder.BuildCadenceClient()
	if err != nil {
		h.Logger.Error("Failed to build cadence client.", zap.Error(err))
		panic(err)
	}

	we, err := workflowClient.SignalWithStartWorkflow(ctx, workflowID, signalName, signalArg, options, workflow, workflowArgs...)
	if err != nil {
		h.Logger.Error("Failed to signal with start workflow", zap.Error(err))
		panic("Failed to signal with start workflow.")

	} else {
		h.Logger.Info("Signaled and started Workflow", zap.String("WorkflowID", we.ID), zap.String("RunID", we.RunID))
	}
}

// StartWorkers starts workflow worker and activity worker based on configured options.
func (h *SampleHelper) StartWorkers(domainName, groupName string, options worker.Options) {
	h.Worker = worker.New(h.Service, domainName, groupName, options)
	err := h.Worker.Start()
	if err != nil {
		h.Logger.Error("Failed to start workers.", zap.Error(err))
		panic("Failed to start workers")
	}
}

// StopWorkers stops workflow worker and activity workers.
func (h *SampleHelper) StopWorkers() {
	h.Worker.Stop()
}

func (h *SampleHelper) QueryWorkflow(workflowID, runID, queryType string, args ...interface{}) {
	workflowClient, err := h.Builder.BuildCadenceClient()
	if err != nil {
		h.Logger.Error("Failed to build cadence client.", zap.Error(err))
		panic(err)
	}

	resp, err := workflowClient.QueryWorkflow(context.Background(), workflowID, runID, queryType, args...)
	if err != nil {
		h.Logger.Error("Failed to query workflow", zap.Error(err))
		panic("Failed to query workflow.")
	}
	var result interface{}
	if err := resp.Get(&result); err != nil {
		h.Logger.Error("Failed to decode query result", zap.Error(err))
	}
	h.Logger.Info("Received query result", zap.Any("Result", result))
}

func (h *SampleHelper) SignalWorkflow(workflowID, signal string, data interface{}) {
	workflowClient, err := h.Builder.BuildCadenceClient()
	if err != nil {
		h.Logger.Error("Failed to build cadence client.", zap.Error(err))
		panic(err)
	}

	err = workflowClient.SignalWorkflow(context.Background(), workflowID, "", signal, data)
	if err != nil {
		h.Logger.Error("Failed to signal workflow", zap.Error(err))
		panic("Failed to signal workflow.")
	}
}

func (h *SampleHelper) CancelWorkflow(workflowID string) {
	workflowClient, err := h.Builder.BuildCadenceClient()
	if err != nil {
		h.Logger.Error("Failed to build cadence client.", zap.Error(err))
		panic(err)
	}

	err = workflowClient.CancelWorkflow(context.Background(), workflowID, "")
	if err != nil {
		h.Logger.Error("Failed to cancel workflow", zap.Error(err))
		panic("Failed to cancel workflow.")
	}
}
