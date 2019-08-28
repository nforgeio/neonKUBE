package main

import (
	"context"
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
	activity.Register(TestActivity)
}

var firstRun = true

func ReplayWorkflow(ctx workflow.Context) (string, error) {

	fmt.Println("==================================")
	printRun()
	printReplayStatus(ctx)

	if firstRun {
		firstRun = false
		forceReplay(ctx)
	}

	fmt.Println("Calling activity #1")
	testActivity(ctx, "#1")
	printReplayStatus(ctx)

	// if firstRun {
	// 	firstRun = false
	// 	forceReplay(ctx)
	// }

	fmt.Println("Calling activity #2")
	testActivity(ctx, "#3")
	printReplayStatus(ctx)

	return "Completed", nil
}

func printRun() {
	fmt.Println("----------")
	if firstRun {
		fmt.Println("FIRST RUN")
	} else {
		fmt.Println("SECOND RUN")
	}
}

func printReplayStatus(ctx workflow.Context) {
	if workflow.IsReplaying(ctx) {
		fmt.Println("IsReplaying: TRUE")
	} else {
		fmt.Println("IsReplaying: FALSE")
	}
}

func forceReplay(ctx workflow.Context) {
	fmt.Println("*** Force Replay ***")
	//workflow.Sleep(ctx, 0)
	//workflow.NewTimer(ctx, 0).Get(ctx, nil)
	panic("Force Replay")
	//fmt.Println("*** Force DONE ***")
}

func TestActivity(ctx context.Context, value string) (string, error) {
	return value, nil
}

//-----------------------------------------------------------------------------
// Helpers

// This needs to be done as part of a bootstrap step when the process starts.
// The workers are supposed to be long running.
func startWorkers(h *common.SampleHelper) worker.Worker {
	// Configure worker options.
	workerOptions := worker.Options{
		MetricsScope:           h.Scope,
		Logger:                 h.Logger,
		DisableStickyExecution: false,
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

func testActivity(ctx workflow.Context, value string) (string, error) {

	var result string
	var err error

	ao := workflow.ActivityOptions{
		ScheduleToCloseTimeout: time.Second * 60,
		ScheduleToStartTimeout: time.Second * 60,
		StartToCloseTimeout:    time.Second * 60,
		HeartbeatTimeout:       time.Second * 10,
		WaitForCancellation:    false,
	}

	ctx = workflow.WithActivityOptions(ctx, ao)
	future := workflow.ExecuteActivity(ctx, TestActivity, value)
	err = future.Get(ctx, &result)

	return result, err
}
