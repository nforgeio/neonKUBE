package main

import (
	"context"
	"fmt"
	"io/ioutil"
	"strconv"
	"time"

	"go.temporal.io/sdk/worker"
	"go.uber.org/zap"
	"gopkg.in/yaml.v2"

	"go.temporal.io/sdk/client"
)

type (
	// SampleHelper class for workflow sample helper.
	SampleHelper struct {
		clientOptions   client.Options
		Logger          *zap.Logger
		NamespaceClient client.NamespaceClient
		WorkflowClient  client.Client
		Worker          worker.Worker
		clientTimeout   time.Duration
		Config          Configuration
	}

	// Configuration for running samples.
	Configuration struct {
		Namespace   string `yaml:"namespace"`
		ServiceName string `yaml:"service"`
		HostPort    string `yaml:"hostport"`
	}
)

// SetupServiceConfig setup the config for the sample code run
func (h *SampleHelper) SetupServiceConfig(configPath string) error {

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

	// TODO: JACK -  Make sure that we figure this out.  Hack to get
	// debugging working.
	h.clientOptions.ConnectionOptions.DisableHealthCheck = true

	namespaceClient, err := client.NewNamespaceClient(h.clientOptions)
	if err != nil {

		h.Logger.Error("failed to create Temporal namespace client",
			zap.String("HostPort", h.clientOptions.HostPort),
			zap.Error(err))

		return err
	}

	h.Logger.Info("successfully created Temporal namespace client",
		zap.String("HostPort", h.clientOptions.HostPort))

	h.NamespaceClient = namespaceClient

	h.WorkflowClient, err = client.NewClient(h.clientOptions)
	if err != nil {
		h.Logger.Error("Failed to create Temporal client",
			zap.String("Namespace", h.clientOptions.Namespace),
			zap.String("HostPort", h.clientOptions.HostPort),
			zap.Error(err))

		return err
	}

	h.Logger.Info("Successfully created temporal client",
		zap.String("Namespace", h.clientOptions.Namespace),
		zap.String("HostPort", h.clientOptions.HostPort))

	return nil
}

// Executes a workflow returning a string and waits for it to complete.
func (h *SampleHelper) ExecuteWorkflow(options client.StartWorkflowOptions, workflow interface{}, args ...interface{}) {
	workflowClient := h.WorkflowClient
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
	workflowClient := h.WorkflowClient
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

// Executes a workflow the returns an error and waits for it to complete.
func (h *SampleHelper) ExecuteErrorWorkflow(options client.StartWorkflowOptions, workflow interface{}, args ...interface{}) {
	workflowClient := h.WorkflowClient
	ctx := context.Background()

	wr, err := workflowClient.ExecuteWorkflow(ctx, options, workflow, args...)
	if err != nil {
		h.Logger.Error("Failed to create workflow", zap.Error(err))
		panic("Failed to create workflow.")

	} else {
		h.Logger.Info("Started Workflow", zap.String("RunID", wr.GetRunID()))
	}

	err = wr.Get(ctx, nil)

	if err != nil {
		h.Logger.Info("Error:  " + err.Error())
	}
}

// Executes a workflow returning a string and an error and waits for it to complete.
func (h *SampleHelper) ExecuteStringErrorWorkflow(options client.StartWorkflowOptions, workflow interface{}, args ...interface{}) {
	workflowClient := h.WorkflowClient
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

	if err == nil {
		h.Logger.Info("Result: " + result)
	} else {
		h.Logger.Info("Error:  " + err.Error())
	}
}

// StartWorkflow starts a workflow
func (h *SampleHelper) StartWorkflow(options client.StartWorkflowOptions, workflow interface{}, args ...interface{}) {
	h.StartWorkflowWithCtx(context.Background(), options, workflow, args...)
}

// StartWorkflowWithCtx starts a workflow with the provided context
func (h *SampleHelper) StartWorkflowWithCtx(ctx context.Context, options client.StartWorkflowOptions, workflow interface{}, args ...interface{}) {
	workflowClient := h.WorkflowClient
	run, err := workflowClient.ExecuteWorkflow(ctx, options, workflow, args...)
	if err != nil {
		h.Logger.Error("Failed to create workflow", zap.Error(err))
		panic("Failed to create workflow.")

	} else {
		h.Logger.Info("Started Workflow", zap.String("WorkflowID", run.GetID()), zap.String("RunID", run.GetRunID()))
	}
}

// SignalWithStartWorkflowWithCtx signals workflow and starts it if it's not yet started
func (h *SampleHelper) SignalWithStartWorkflowWithCtx(ctx context.Context, workflowID string, signalName string, signalArg interface{},
	options client.StartWorkflowOptions, workflow interface{}, workflowArgs ...interface{}) {
	workflowClient := h.WorkflowClient
	run, err := workflowClient.SignalWithStartWorkflow(ctx, workflowID, signalName, signalArg, options, workflow, workflowArgs...)
	if err != nil {
		h.Logger.Error("Failed to signal with start workflow", zap.Error(err))
		panic("Failed to signal with start workflow.")

	} else {
		h.Logger.Info("Signaled and started Workflow", zap.String("WorkflowID", run.GetID()), zap.String("RunID", run.GetRunID()))
	}
}

// StartWorkers starts workflow worker and activity worker based on configured options.
func (h *SampleHelper) StartWorkers(domainName, groupName string, options worker.Options) {
	h.Worker = worker.New(h.WorkflowClient, domainName, options)
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
	workflowClient := h.WorkflowClient
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
	workflowClient := h.WorkflowClient
	err := workflowClient.SignalWorkflow(context.Background(), workflowID, "", signal, data)
	if err != nil {
		h.Logger.Error("Failed to signal workflow", zap.Error(err))
		panic("Failed to signal workflow.")
	}
}

func (h *SampleHelper) CancelWorkflow(workflowID string) {
	workflowClient := h.WorkflowClient
	err := workflowClient.CancelWorkflow(context.Background(), workflowID, "")
	if err != nil {
		h.Logger.Error("Failed to cancel workflow", zap.Error(err))
		panic("Failed to cancel workflow.")
	}
}
