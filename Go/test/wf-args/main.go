package main

import (
	"time"

	"github.com/pborman/uuid"
	"go.uber.org/cadence/activity"
	"go.uber.org/cadence/client"
	"go.uber.org/cadence/worker"
	"go.uber.org/cadence/workflow"
)

const taskList = "gotest-args"

func main() {

	runWorkflows := false

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

	if (runWorkflows) {

		//-----------------------------------------------------
		// NoArgs

		workflowOptions := client.StartWorkflowOptions{
			ID:                              "noArgs_" + uuid.New(),
			TaskList:                        taskList,
			ExecutionStartToCloseTimeout:    time.Minute,
			DecisionTaskStartToCloseTimeout: time.Minute,
		}

		h.StartWorkflow(workflowOptions, NoArgsWorkflow)

		//-----------------------------------------------------
		// OneArg

		workflowOptions = client.StartWorkflowOptions{
			ID:                              "oneArg_" + uuid.New(),
			TaskList:                        taskList,
			ExecutionStartToCloseTimeout:    time.Minute,
			DecisionTaskStartToCloseTimeout: time.Minute,
		}

		h.StartWorkflow(workflowOptions, OneArgWorkflow, "CADENCE")

		//-----------------------------------------------------
		// TwoArgs

		workflowOptions = client.StartWorkflowOptions{
			ID:                              "twoArgs_" + uuid.New(),
			TaskList:                        taskList,
			ExecutionStartToCloseTimeout:    time.Minute,
			DecisionTaskStartToCloseTimeout: time.Minute,
		}

		h.StartWorkflow(workflowOptions, TwoArgsWorkflow, "JACK", "JILL")
	}

	//-----------------------------------------------------
	// Give the workflows a chance to complete or unit tests
	// a chance to run.

	time.Sleep(10 * time.Second)
}

//-----------------------------------------------------------
// set GO111MODULE=on
// go get go.uber.org/cadence
// go build -o C:\src\neonKUBE\Build\go-test\wf-args.exe .
// cp config.yaml C:\src\neonKUBE\Build\go-test\config.yaml
