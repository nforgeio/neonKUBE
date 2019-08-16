package main

import (
	"context"
	"errors"
	"fmt"
	"time"

	"go.uber.org/cadence"
	"go.uber.org/cadence/activity"
	"go.uber.org/cadence/client"
	"go.uber.org/cadence/worker"
	"go.uber.org/cadence/workflow"
	"go.uber.org/zap"

	"github.com/pborman/uuid"

	"github.com/cadence-proxy/cmd/playground/common"
)

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

//-----------------------------------------------------------------------------
// Test workflow and activity

const ApplicationName = "simpleGroup"

func init() {
	workflow.Register(ReplayWorkflow)
	activity.Register(getNameActivity)
}

var firstRun = true

func ReplayWorkflow(ctx workflow.Context) (string, error) {

	fmt.Println("----------")

	if firstRun {
		fmt.Println("FIRST RUN")
	} else {
		fmt.Println("SECOND RUN")
	}

	if workflow.IsReplaying(ctx) {
		fmt.Println("IsReplaying: TRUE")
	} else {
		fmt.Println("IsReplaying: FALSE")
	}

	if firstRun {
		firstRun = false
		//time.Sleep(time.Second * 12)
		return "", errors.New("Force replay.")
	}

	fmt.Println("SLEEPING...")
	workflow.Sleep(ctx, time.Second)

	if workflow.IsReplaying(ctx) {
		fmt.Println("IsReplaying: TRUE")
	} else {
		fmt.Println("IsReplaying: FALSE")
	}

	return "Completed", nil
}

func getNameActivity() (string, error) {
	return "Cadence", nil
}

//-----------------------------------------------------------------------------
// Helpers

// This needs to be done as part of a bootstrap step when the process starts.
// The workers are supposed to be long running.
func startWorkers(h *common.SampleHelper) worker.Worker {
	// Configure worker options.
	workerOptions := worker.Options{
		MetricsScope: h.Scope,
		Logger:       h.Logger,
	}
	return h.StartWorkers(h.Config.DomainName, ApplicationName, workerOptions)
}

func startWorkflow(h *common.SampleHelper) client.WorkflowRun {

	workflowOptions := client.StartWorkflowOptions{
		ID:                              "simple_" + uuid.New(),
		TaskList:                        ApplicationName,
		ExecutionStartToCloseTimeout:    time.Minute,
		DecisionTaskStartToCloseTimeout: time.Second * 10,
		RetryPolicy: &cadence.RetryPolicy{
			InitialInterval:    time.Second,
			BackoffCoefficient: 1.0,
			MaximumInterval:    time.Second,
			ExpirationInterval: time.Minute,
			MaximumAttempts:    2},
	}

	return h.StartWorkflow(workflowOptions, ReplayWorkflow)
}
