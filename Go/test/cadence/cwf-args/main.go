package main

import (
	"flag"
	"os"
	"time"

	"github.com/pborman/uuid"
	"go.uber.org/cadence/activity"
	"go.uber.org/cadence/client"
	"go.uber.org/cadence/worker"
	"go.uber.org/cadence/workflow"
)

func main() {

	test := true

	// Parse the -config=<path> option.  This option is required.

	var configPath string

	flag.StringVar(&configPath, "config", "", "Path to the Cadence Server configuration file.")

	// Parse the -stopfile=<path> command line option which specifies
	// the path to the file whose existence will stop execution of this
	// program as well as the -readyfile<path> option which is the
	// path to the file that will be created after the worker has
	// been started.

	var stopFile string
	var readyFile string

	flag.StringVar(&stopFile, "stopfile", "", "Path to the program stop file.")
	flag.StringVar(&readyFile, "readyfile", "", "Path to the program ready file.")

	// Parse the -tasklist=<name> command line option which specifies the
	// Cadence tasklist where the workflows and activities will be registered.
	// This defaults to "wf-args".

	var taskList string

	flag.StringVar(&taskList, "tasklist", "wf-args", "Target Cadence task list.")

	// Parse the -domain=<name> command line option which specifies the
	// Cadence domain where the workflows and activities will be registered.
	// This defaults to "test-domain".

	var domain string

	flag.StringVar(&domain, "domain", "test-domain", "Target Cadence domain.")

	// Parse and verify the command line options.

	flag.Parse()

	if configPath == "" {
		panic("-config option is required.")
	}

	// Register the workflows and activities and start the worker.

	var h SampleHelper

	h.SetupServiceConfig(configPath)

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
	workflow.Register(ArrayArgWorkflow)
	activity.Register(ArrayArgActivity)
	workflow.Register(ArrayArgsWorkflow)
	activity.Register(ArrayArgsActivity)
	workflow.Register(ErrorWorkflow)
	workflow.Register(StringErrorWorkflow)

	h.StartWorkers(domain, taskList, workerOptions)

	if test {

		//-----------------------------------------------------
		// NoArgs

		workflowOptions := client.StartWorkflowOptions{
			ID:                              "TEST:NoArgs-" + uuid.New(),
			TaskList:                        taskList,
			ExecutionStartToCloseTimeout:    time.Minute,
			DecisionTaskStartToCloseTimeout: time.Minute,
		}

		h.ExecuteWorkflow(workflowOptions, NoArgsWorkflow)

		//-----------------------------------------------------
		// OneArg

		workflowOptions = client.StartWorkflowOptions{
			ID:                              "TEST:OneArg-" + uuid.New(),
			TaskList:                        taskList,
			ExecutionStartToCloseTimeout:    time.Minute,
			DecisionTaskStartToCloseTimeout: time.Minute,
		}

		h.ExecuteWorkflow(workflowOptions, OneArgWorkflow, "CADENCE")

		//-----------------------------------------------------
		// TwoArgs

		workflowOptions = client.StartWorkflowOptions{
			ID:                              "TEST:TwoArgs-" + uuid.New(),
			TaskList:                        taskList,
			ExecutionStartToCloseTimeout:    time.Minute,
			DecisionTaskStartToCloseTimeout: time.Minute,
		}

		h.ExecuteWorkflow(workflowOptions, TwoArgsWorkflow, "JACK", "JILL")

		//-----------------------------------------------------
		// ArrayArg

		workflowOptions = client.StartWorkflowOptions{
			ID:                              "TEST:ArrayArg-" + uuid.New(),
			TaskList:                        taskList,
			ExecutionStartToCloseTimeout:    time.Minute,
			DecisionTaskStartToCloseTimeout: time.Minute,
		}

		h.ExecuteArrayWorkflow(workflowOptions, ArrayArgWorkflow, []int32{0, 1, 2, 3, 4})

		//-----------------------------------------------------
		// ArrayArgs

		workflowOptions = client.StartWorkflowOptions{
			ID:                              "TEST:ArrayArgs-" + uuid.New(),
			TaskList:                        taskList,
			ExecutionStartToCloseTimeout:    time.Minute,
			DecisionTaskStartToCloseTimeout: time.Minute,
		}

		h.ExecuteArrayWorkflow(workflowOptions, ArrayArgsWorkflow, []int32{0, 1, 2, 3, 4}, "test")

		//-----------------------------------------------------
		// Error Tests

		workflowOptions = client.StartWorkflowOptions{
			ID:                              "TEST:ErrTest-NOERROR-" + uuid.New(),
			TaskList:                        taskList,
			ExecutionStartToCloseTimeout:    time.Minute,
			DecisionTaskStartToCloseTimeout: time.Minute,
		}

		h.ExecuteErrorWorkflow(workflowOptions, ErrorWorkflow, "")

		workflowOptions = client.StartWorkflowOptions{
			ID:                              "TEST:ErrTest-ERROR-" + uuid.New(),
			TaskList:                        taskList,
			ExecutionStartToCloseTimeout:    time.Minute,
			DecisionTaskStartToCloseTimeout: time.Minute,
		}

		h.ExecuteErrorWorkflow(workflowOptions, ErrorWorkflow, "error-message")

		workflowOptions = client.StartWorkflowOptions{
			ID:                              "TEST:ResultErrTest-RESULT-" + uuid.New(),
			TaskList:                        taskList,
			ExecutionStartToCloseTimeout:    time.Minute,
			DecisionTaskStartToCloseTimeout: time.Minute,
		}

		h.ExecuteStringErrorWorkflow(workflowOptions, StringErrorWorkflow, "CADENCE", "")

		workflowOptions = client.StartWorkflowOptions{
			ID:                              "TEST:ResultErrTest-ERROR-" + uuid.New(),
			TaskList:                        taskList,
			ExecutionStartToCloseTimeout:    time.Minute,
			DecisionTaskStartToCloseTimeout: time.Minute,
		}

		h.ExecuteStringErrorWorkflow(workflowOptions, StringErrorWorkflow, "", "error message")
	}

	//-----------------------------------------------------
	// Indicate to the calling unit test that the worker is ready.

	if readyFile != "" {

		f, err := os.Create(readyFile)

		if err != nil {
			h.Logger.Info("BAD READY FILE: " + readyFile)
			return
		}

		f.Close()
	}

	//-----------------------------------------------------
	// Process workflows and activities until the stop file is
	// created or for 60 seconds when no stop file was specified.

	if stopFile != "" {

		h.Logger.Info("STOP FILE: " + stopFile)

		for {

			if _, err := os.Stat(stopFile); err == nil {

				h.Logger.Info("STOP SIGNALLED")
				break
			}

			time.Sleep(100 * time.Millisecond)
		}

	} else {

		h.Logger.Info("NO STOP FILE")
		time.Sleep(60 * time.Second)
	}

	// Stop the Cadence worker gracefully.

	h.Logger.Info("STOPPING...")
	h.StopWorkers()
}
