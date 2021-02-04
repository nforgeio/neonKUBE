package main

import (
	"flag"
	"os"
	"time"

	"github.com/pborman/uuid"
	"go.temporal.io/sdk/client"
	"go.temporal.io/sdk/worker"
)

func main() {

	test := false

	// Parse the -config=<path> option.  This option is required.

	var configPath string

	flag.StringVar(&configPath, "config", "", "Path to the temporal Server configuration file.")

	// Parse the -stopfile=<path> command line option which specifies
	// the path to the file whose existence will stop execution of this
	// program as well as the -readyfile<path> option which is the
	// path to the file that will be created after the worker has
	// been started.

	var stopFile string
	var readyFile string

	flag.StringVar(&stopFile, "stopfile", "", "Path to the program stop file.")
	flag.StringVar(&readyFile, "readyfile", "", "Path to the program ready file.")

	// Parse the -taskqueue=<name> command line option which specifies the
	// Temporal taskqueue where the workflows and activities will be registered.
	// This defaults to "wf-args".

	var taskQueue string

	flag.StringVar(&taskQueue, "taskqueue", "wf-args", "Target Temporal task list.")

	// Parse the -namespace=<name> command line option which specifies the
	// Temporal namespace where the workflows and activities will be registered.
	// This defaults to "test-namespace".

	var namespace string

	flag.StringVar(&namespace, "namespace", "test-namespace", "Target Temporal namespace.")

	// Parse and verify the command line options.

	flag.Parse()

	if configPath == "" {
		panic("-config option is required.")
	}

	// Register the workflows and activities and start the worker.

	var h SampleHelper

	h.SetupServiceConfig(configPath)

	workerOptions := worker.Options{}

	h.CreateWorker(taskQueue, workerOptions)

	h.Worker.RegisterWorkflow(NoArgsWorkflow)
	h.Worker.RegisterActivity(NoArgsActivity)
	h.Worker.RegisterWorkflow(OneArgWorkflow)
	h.Worker.RegisterActivity(OneArgActivity)
	h.Worker.RegisterWorkflow(TwoArgsWorkflow)
	h.Worker.RegisterActivity(TwoArgsActivity)
	h.Worker.RegisterWorkflow(ArrayArgWorkflow)
	h.Worker.RegisterActivity(ArrayArgActivity)
	h.Worker.RegisterWorkflow(ArrayArgsWorkflow)
	h.Worker.RegisterActivity(ArrayArgsActivity)
	h.Worker.RegisterWorkflow(ErrorWorkflow)
	h.Worker.RegisterWorkflow(StringErrorWorkflow)

	h.StartWorker()

	if test {

		//-----------------------------------------------------
		// NoArgs

		workflowOptions := client.StartWorkflowOptions{
			ID:                       "TEST:NoArgs-" + uuid.New(),
			TaskQueue:                taskQueue,
			WorkflowExecutionTimeout: time.Minute,
			WorkflowTaskTimeout:      time.Minute,
		}

		h.ExecuteWorkflow(workflowOptions, NoArgsWorkflow)

		//-----------------------------------------------------
		// OneArg

		workflowOptions = client.StartWorkflowOptions{
			ID:                       "TEST:OneArg-" + uuid.New(),
			TaskQueue:                taskQueue,
			WorkflowExecutionTimeout: time.Minute,
			WorkflowTaskTimeout:      time.Minute,
		}

		h.ExecuteWorkflow(workflowOptions, OneArgWorkflow, "Temporal")

		//-----------------------------------------------------
		// TwoArgs

		workflowOptions = client.StartWorkflowOptions{
			ID:                       "TEST:TwoArgs-" + uuid.New(),
			TaskQueue:                taskQueue,
			WorkflowExecutionTimeout: time.Minute,
			WorkflowTaskTimeout:      time.Minute,
		}

		h.ExecuteWorkflow(workflowOptions, TwoArgsWorkflow, "JACK", "JILL")

		//-----------------------------------------------------
		// ArrayArg

		workflowOptions = client.StartWorkflowOptions{
			ID:                       "TEST:ArrayArg-" + uuid.New(),
			TaskQueue:                taskQueue,
			WorkflowExecutionTimeout: time.Minute,
			WorkflowTaskTimeout:      time.Minute,
		}

		h.ExecuteArrayWorkflow(workflowOptions, ArrayArgWorkflow, []int32{0, 1, 2, 3, 4})

		//-----------------------------------------------------
		// ArrayArgs

		workflowOptions = client.StartWorkflowOptions{
			ID:                       "TEST:ArrayArgs-" + uuid.New(),
			TaskQueue:                taskQueue,
			WorkflowExecutionTimeout: time.Minute,
			WorkflowTaskTimeout:      time.Minute,
		}

		h.ExecuteArrayWorkflow(workflowOptions, ArrayArgsWorkflow, []int32{0, 1, 2, 3, 4}, "test")

		//-----------------------------------------------------
		// Error Tests

		workflowOptions = client.StartWorkflowOptions{
			ID:                       "TEST:ErrTest-NOERROR-" + uuid.New(),
			TaskQueue:                taskQueue,
			WorkflowExecutionTimeout: time.Minute,
			WorkflowTaskTimeout:      time.Minute,
		}

		h.ExecuteErrorWorkflow(workflowOptions, ErrorWorkflow, "")

		workflowOptions = client.StartWorkflowOptions{
			ID:                       "TEST:ErrTest-ERROR-" + uuid.New(),
			TaskQueue:                taskQueue,
			WorkflowExecutionTimeout: time.Minute,
			WorkflowTaskTimeout:      time.Minute,
		}

		h.ExecuteErrorWorkflow(workflowOptions, ErrorWorkflow, "error-message")

		workflowOptions = client.StartWorkflowOptions{
			ID:                       "TEST:ResultErrTest-RESULT-" + uuid.New(),
			TaskQueue:                taskQueue,
			WorkflowExecutionTimeout: time.Minute,
			WorkflowTaskTimeout:      time.Minute,
		}

		h.ExecuteStringErrorWorkflow(workflowOptions, StringErrorWorkflow, "Temporal", "")

		workflowOptions = client.StartWorkflowOptions{
			ID:                       "TEST:ResultErrTest-ERROR-" + uuid.New(),
			TaskQueue:                taskQueue,
			WorkflowExecutionTimeout: time.Minute,
			WorkflowTaskTimeout:      time.Minute,
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

	// Stop the Temporal worker gracefully.

	h.Logger.Info("STOPPING...")
	h.StopWorkers()
}
