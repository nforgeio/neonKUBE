package main

import (
	"context"
	"time"

	"go.uber.org/cadence/client"
	"go.uber.org/cadence/worker"
	"go.uber.org/zap"

	"github.com/pborman/uuid"

	"github.com/cadence-proxy/cmd/playground/common"
)

func startWorkers(h *common.SampleHelper) worker.Worker {
	logger := h.Logger.Named("cadence")
	workerOptions := worker.Options{
		MetricsScope: h.Scope,
		Logger:       logger,
	}
	return h.StartWorkers(h.Config.DomainName, ApplicationName, workerOptions)
}

func startWorkflow(h *common.SampleHelper) client.WorkflowRun {
	workflowOptions := client.StartWorkflowOptions{
		ID:                              "simple_" + uuid.New(),
		TaskList:                        ApplicationName,
		ExecutionStartToCloseTimeout:    time.Minute,
		DecisionTaskStartToCloseTimeout: time.Minute,
	}
	return h.StartWorkflow(workflowOptions, SampleWorkflow)
}

func main() {

	// setup the SampleHelper
	var h common.SampleHelper
	h.SetupServiceConfig()

	// start the worker
	// execute the workflow
	workflowWorker := startWorkers(&h)
	workflowRun := startWorkflow(&h)

	// build the workflow client
	workflowClient, err := h.Builder.BuildCadenceClient()
	if err != nil {
		panic(err)
	}

	// create the context
	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()

	// describe the workflow execution
	resp, err := workflowClient.DescribeWorkflowExecution(ctx, workflowRun.GetID(), workflowRun.GetRunID())
	if err != nil {
		panic(err)
	}

	// log response
	h.Logger.Debug("Resp", zap.Any("Execution Info", *resp))

	// create context to
	// get the workflow result
	var result string
	err = workflowRun.Get(ctx, &result)
	if err != nil {
		h.Logger.Error("workflow failed", zap.Error(err))
	} else {
		h.Logger.Info("Workflow Completed", zap.String("Result", result))
	}

	// stop the worker
	workflowWorker.Stop()
}
