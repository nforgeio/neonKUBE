package main

import (
	"flag"
	"time"

	"github.com/pborman/uuid"
	"go.uber.org/cadence/activity"
	"go.uber.org/cadence/client"
	"go.uber.org/cadence/worker"
	"go.uber.org/cadence/workflow"
)

const taskList = "gotest-args"

func main() {

	// Parse the -wait=<seconds> command line option which specifies
	// how long the program should execute and process workflows and
	// activities.  This defaults to 10 seconds.

	var waitSeconds int64

	flag.Int64Var(&waitSeconds, "wait", 10, "Program execution time in seconds.")
	flag.Parse()

	var h SampleHelper

	h.SetupServiceConfig()

	workerOptions := worker.Options{
		MetricsScope: h.Scope,
		Logger:       h.Logger,
	}

	workflow.Register(NoArgsWorkflow)
	activity.Register(NoArgsActivity)
	workflow.Register(OneArgWorkflow)
	activity.Register(OneArgActivity)
	workflow.Register(TwoArgsWorkflow)
	activity.Register(TwoArgsActivity)

	h.StartWorkers("test-domain", taskList, workerOptions)

	//-----------------------------------------------------
	// NoArgs

	workflowOptions := client.StartWorkflowOptions{
		ID:                              "noArgs_" + uuid.New(),
		TaskList:                        taskList,
		ExecutionStartToCloseTimeout:    time.Minute,
		DecisionTaskStartToCloseTimeout: time.Minute,
	}

	//h.StartWorkflow(workflowOptions, NoArgsWorkflow)

	//-----------------------------------------------------
	// OneArg

	workflowOptions = client.StartWorkflowOptions{
		ID:                              "oneArg_" + uuid.New(),
		TaskList:                        taskList,
		ExecutionStartToCloseTimeout:    time.Minute,
		DecisionTaskStartToCloseTimeout: time.Minute,
	}

	//h.StartWorkflow(workflowOptions, OneArgWorkflow, "CADENCE")

	//-----------------------------------------------------
	// TwoArgs

	workflowOptions = client.StartWorkflowOptions{
		ID:                              "twoArgs_" + uuid.New(),
		TaskList:                        taskList,
		ExecutionStartToCloseTimeout:    time.Minute,
		DecisionTaskStartToCloseTimeout: time.Minute,
	}

	h.StartWorkflow(workflowOptions, TwoArgsWorkflow, "JACK", "JILL")

	//-----------------------------------------------------
	// Process workflows and activities for the specified
	// wait time.

	time.Sleep(time.Duration(waitSeconds) * time.Second)
}
