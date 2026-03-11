# SignalWeave Modern User Manual

This manual is the full reference for the `Modern` SignalWeave workflow.

Use this document when you want to understand:

- what each section of the application is for
- what every setting means
- what each action changes
- what gets saved in the project file
- how the training, testing, weight inspection, and analysis tools fit together

For a shorter “get started fast” walkthrough, use [modern-ui-guide.md](/home/simon/signalweave/docs/modern-ui-guide.md).

## Manual plan

This manual is organized in the same order a user sees the application:

1. core concepts
2. overall layout
3. top toolbar
4. left workspace tabs
5. right workflow tabs
6. bottom panels
7. common workflows
8. project file contents
9. practical interpretation tips
10. troubleshooting notes

## 1. Core concepts

Before going section by section, it helps to define the main objects the app works with.

### Project

A `SignalWeave Modern` project is the main working document.

What it does:
- stores the current network definition
- stores the current pattern set
- stores the current weights
- stores completed training cycles
- stores training sessions and rollback checkpoints

What it affects:
- what topology is shown in the graph
- what patterns can be trained and tested
- what weights are used by training, testing, and analysis
- what history is available for rollback and comparison

What it represents:
- the full current state of a modeling session, not just one file fragment

### Network definition

The network definition describes the architecture and training parameters of the active network.

What it does:
- defines the network kind
- defines the layer sizes
- defines bias usage
- defines training defaults such as learning rate and error function

What it affects:
- graph layout
- weight matrix shapes
- training behavior
- test output dimensionality
- analysis options

What it represents:
- the “shape and rules” of the model being trained

### Pattern set

The pattern set is the dataset stored in the project.

What it does:
- provides the inputs and targets used for training and testing
- optionally defines sequence boundaries through reset markers

What it affects:
- what the model learns from
- what can be tested
- what appears in the Tests and Patterns tabs
- what analysis results can be generated

What it represents:
- the current training/testing dataset

### Weights

Weights are the learned connection values inside the network.

What they do:
- determine how inputs are transformed into hidden activations and outputs

What they affect:
- every result in training, testing, graph coloring, weight inspection, and analysis

What they represent:
- the current learned state of the model

### Training session

A training session is a visible checkpoint created after one explicit training run.

What it does:
- captures the weights after a `Train #N` run
- stores the number of steps executed
- stores completed cycles
- stores the displayed average error
- stores the session’s error history

What it affects:
- rollback availability
- weight inspection source selection
- analysis source selection
- error graph history selection

What it represents:
- one user-visible training milestone

### Test result

A test result is the output of the current model for one pattern or for all patterns.

What it does:
- stores inputs, targets, outputs, hidden activations, and error for a specific pattern

What it affects:
- pass/fail indicators
- the pattern inspector
- the network graph activation overlay
- hidden-activation export

What it represents:
- what the model currently does for a specific example

## 2. Overall layout

The Modern UI is split into three main zones:

- top toolbar
- middle workspace
- bottom output area

The middle workspace is split into:

- left: `Network Graph`, `Weights`, `Analysis`
- right: `Network`, `Training`, `Tests`, `Patterns`, `Summary`

The bottom area is split into:

- `Console`
- `Error graph`

All of these areas work on the same active project.

## 3. Top toolbar

The toolbar contains global project actions and status.

### Product/version label

Example:
- `SignalWeave Modern v1.0.0`

What it does:
- identifies the current product line and version

What it affects:
- nothing in the model or workflow

What it represents:
- the running application version

### `✚ New`

What it does:
- creates a fresh project

What it affects:
- clears the current working model state
- resets the project to a default sample-style starting point

What it represents:
- start over with a new working document

### `📂 Load`

What it does:
- opens an existing `.swproj.json` project file

What it affects:
- replaces the current project state with the loaded one

What it represents:
- continue an existing session

### `💾 Save`

What it does:
- saves the active project back to its current file path

What it affects:
- persists the current project state

What it represents:
- commit the current state to disk

### `🖫 Save As`

What it does:
- saves the current project to a new file path

What it affects:
- creates a separate saved copy of the current state

What it represents:
- branch your work into a new project file

### Workspace status text

Examples:
- `Ready.`
- `Working in an unsaved project.`
- `Loaded project: something.swproj.json`

What it does:
- reports the latest high-level application status

What it affects:
- nothing directly

What it represents:
- the current file/workspace condition

## 4. Left workspace tabs

These tabs are the main visual and analytical workspace.

## 4.1 Network Graph

This tab shows the active topology and, when test results are available, the current activation overlay.

### Graph title: `Network graph`

What it does:
- labels the primary visual workspace

What it affects:
- nothing

What it represents:
- the model topology view

### Graph summary text

Examples:
- `3-layer topology`
- `Showing activations for pattern 4: pattern-4`

What it does:
- summarizes what the graph is currently showing

What it affects:
- nothing

What it represents:
- either plain topology mode or a specific test-result view

### `🖫` save graph button

What it does:
- saves the visible network graph as a PNG image

What it affects:
- creates an external export file only

What it represents:
- export the current visual state of the network graph

### Graph canvas

What it does:
- draws the network structure
- colors connection lines by weight sign and magnitude
- overlays activations when a test result is selected

What it affects:
- visual interpretation only

What it represents:
- the active model structure, optionally combined with the latest selected pattern result

### Weight gradient scale

The scale at the bottom of the graph shows the meaning of line colors.

What it does:
- maps negative-to-positive weights onto a color gradient

What it affects:
- how you interpret edge colors in the graph

What it represents:
- the current weight range reference for line coloring

Interpretation:
- negative end: red
- zero: dark/neutral
- positive end: green

## 4.2 Weights

This tab is the dedicated weight inspector.

It exists because the network graph is good for a quick visual read, but not good enough for detailed weight inspection once the network grows.

### Source

Options:
- `Current`
- `Train #N`

What it does:
- selects which weight set to inspect

What it affects:
- the matrix currently shown
- the stats currently shown

What it represents:
- either the live model now or a historical training checkpoint

### Layer

Possible examples:
- `Input → Output`
- `Input → Hidden`
- `Input → Hidden 1`
- `Hidden 1 → Hidden 2`
- `Hidden 1 → Output`
- `Hidden 2 → Output`
- `Context → Hidden`

What it does:
- chooses which weight matrix to inspect

What it affects:
- the matrix shown in the canvas
- the labels used for rows and columns

What it represents:
- one specific connection set in the active network

The available layers depend on topology:
- direct feed-forward networks expose `Input → Output`
- 3-layer feed-forward exposes input-to-hidden and hidden-to-output
- 4-layer feed-forward adds hidden-to-hidden and second-hidden-to-output
- simple recurrent networks add `Context → Hidden`

### View

Options:
- `Heatmap`
- `Magnitude`
- `Values`

What it does:
- changes how the selected weight matrix is rendered

What it affects:
- the visual rendering mode of the weight canvas

What it represents:
- three different ways to read the same weights

Interpretation:
- `Heatmap`: emphasizes sign and relative scale
- `Magnitude`: emphasizes strength regardless of sign
- `Values`: emphasizes exact numeric values

### Weight stats panel

The stats panel reports summary information such as:
- rows
- columns
- min
- max
- mean absolute weight
- near-zero count

What it does:
- summarizes the currently selected matrix numerically

What it affects:
- nothing directly

What it represents:
- the scale and sparsity of the selected weight set

### Weight canvas

What it does:
- renders the selected weight matrix visually

What it affects:
- visual inspection only

What it represents:
- the chosen matrix in the selected view mode

## 4.3 Analysis

This tab contains dataset-level analysis and charting tools.

It answers questions that are broader than a single pattern:
- how outputs cluster
- how hidden states cluster
- how signals vary over the pattern sequence
- how a chosen output/target/hidden unit behaves over a 2D input plane

### Mode

Options:
- `Output clustering`
- `Hidden-state clustering`
- `Compatibility summary`
- `Time series`
- `Surface plot`

What it does:
- chooses which analysis tool to run

What it affects:
- which controls appear
- whether the result is text or a chart

What it represents:
- the type of analysis you want to perform

#### Output clustering

What it does:
- clusters patterns based on their output behavior

What it affects:
- the text report output

What it represents:
- similarity relationships between patterns at the model output level

#### Hidden-state clustering

What it does:
- clusters patterns based on hidden activations

What it affects:
- the text report output

What it represents:
- similarity relationships inside the model’s learned internal representation

#### Compatibility summary

What it does:
- displays the engine’s compatibility/profile summary

What it affects:
- the text report output

What it represents:
- a general capability/configuration profile of the current setup

#### Time series

What it does:
- plots one selected signal across the ordered pattern list

What it affects:
- the chart display
- CSV export content

What it represents:
- how one input, target, output, or hidden activation changes over sequence steps

#### Surface plot

What it does:
- builds a 2D grid from two selected inputs and plots a selected target/output/hidden value over that grid

What it affects:
- the chart display
- CSV export content

What it represents:
- how the model responds over a 2D input space

### Source

Options:
- `Current`
- `Train #N`

What it does:
- selects which weight set the analysis runs against

What it affects:
- all report or chart results in the Analysis tab

What it represents:
- whether you are analyzing the current model or a saved checkpoint

### `Run`

What it does:
- executes the selected analysis against the selected source

What it affects:
- refreshes the report or chart

What it represents:
- perform the current analysis request

### `⧉` copy analysis

What it does:
- copies the current analysis output to the clipboard

What it affects:
- clipboard contents only

What it represents:
- quick export for reports or CSV-based chart data

Behavior:
- text-report modes copy report text
- chart modes copy CSV data

### `🖫` save analysis

What it does:
- saves the current analysis output

What it affects:
- creates a file

What it represents:
- persist the current analysis output

Behavior:
- report modes save text
- `Time series` saves CSV
- `Surface plot` saves CSV

### `🖼` save analysis chart

What it does:
- saves the visible analysis chart as PNG

What it affects:
- creates a PNG export

What it represents:
- export the chart image for time-series or surface analysis

### Time series controls

These appear only in `Time series` mode.

#### Series

Options:
- `Input`
- `Target`
- `Output`
- `Hidden`

What it does:
- chooses which kind of signal to plot

What it affects:
- which values are sampled across the run
- how the index selector is populated

What it represents:
- the signal family you want to inspect

#### Index

Examples:
- `Input 1`
- `Output 3`
- `Hidden 2`

What it does:
- chooses the specific member of the selected signal family

What it affects:
- the actual time-series line that gets plotted

What it represents:
- the exact signal dimension being inspected

### Surface plot controls

These appear only in `Surface plot` mode.

#### X input

What it does:
- chooses which input dimension becomes the X axis

What it affects:
- the horizontal dimension of the surface grid

What it represents:
- the first input variable in the 2D plot

#### Y input

What it does:
- chooses which input dimension becomes the Y axis

What it affects:
- the vertical dimension of the surface grid

What it represents:
- the second input variable in the 2D plot

#### Z series

Options:
- `Target`
- `Output`
- `Hidden`

What it does:
- chooses what kind of value is plotted over the X/Y grid

What it affects:
- the surface content
- the index selector contents

What it represents:
- whether the plot shows expected values, actual predictions, or hidden-unit behavior

#### Index

What it does:
- chooses the specific target, output, or hidden unit to plot

What it affects:
- the Z values on the surface

What it represents:
- the exact value dimension being visualized

### Analysis report area

What it does:
- shows clustering and compatibility text reports

What it affects:
- text-mode analysis output only

What it represents:
- human-readable analytical output for non-chart modes

### Analysis chart area

What it does:
- renders time-series or surface-plot charts

What it affects:
- visual chart inspection only

What it represents:
- the chart result for the selected analysis mode

## 5. Right workflow tabs

These tabs are where the project is configured and operated.

## 5.1 Network

This tab defines the model structure and architecture-level settings.

### Project name

What it does:
- sets the project/display name stored in the definition

What it affects:
- summary text
- saved project metadata
- status labeling in some outputs

What it represents:
- the human-readable identity of the current project

### Network type

Options:
- `FeedForward`
- `SimpleRecurrent`

What it does:
- selects the model family

What it affects:
- available layer combinations
- graph layout
- weight-layer options
- analysis hidden-state behavior
- whether recurrent context is used

What it represents:
- whether the model is a plain feed-forward network or a simple recurrent network

#### FeedForward

What it does:
- uses a non-recurrent network

What it affects:
- supports direct, 3-layer, and 4-layer feed-forward layouts

What it represents:
- a model where each pattern is evaluated independently

#### SimpleRecurrent

What it does:
- uses a recurrent hidden-state/context path

What it affects:
- hidden state carries across sequence steps until reset
- second hidden layer is disabled
- recurrent weight matrix becomes available

What it represents:
- a model with sequence context

### Inputs

What it does:
- sets the number of input units

What it affects:
- graph size
- pattern validation
- analysis input selectors
- input-side weight matrix dimensions

What it represents:
- how many input values each pattern contains

### Hidden layer 1

What it does:
- sets the size of the first hidden layer

What it affects:
- graph structure
- hidden-side weight matrix dimensions
- hidden activation count

What it represents:
- the first internal feature layer of the network

Special behavior:
- in `FeedForward`, it can be `0`, which allows a direct input-to-output network
- in `SimpleRecurrent`, it is forced to at least `1`

### Hidden layer 2

What it does:
- sets the size of the second hidden layer

What it affects:
- graph structure
- hidden-to-hidden and hidden-to-output matrix shapes
- hidden activation count

What it represents:
- the second internal feature layer for deeper feed-forward models

Special behavior:
- enabled only for `FeedForward`
- disabled when `Hidden layer 1` is `0`
- automatically forced to `0` for `SimpleRecurrent`

### Outputs

What it does:
- sets the number of output units

What it affects:
- graph size
- pattern validation
- test outputs
- output clustering
- output-side weight dimensions

What it represents:
- how many output values the model produces

### Biases

Bias checkboxes:
- `Input`
- `Hidden 1`
- `Hidden 2`

Biases add constant bias sources to the relevant layer transitions.

#### Input bias

What it does:
- enables a bias term on the input-side transition

What it affects:
- the source matrix dimensions from the input side
- the graph’s bias node/connection structure

What it represents:
- whether the first transition has a constant bias contribution

#### Hidden 1 bias

What it does:
- enables a bias term on the first hidden-layer output transition

What it affects:
- the source dimensions for the hidden-layer outgoing matrix

What it represents:
- whether the first hidden layer contributes through a constant bias source

Special behavior:
- disabled automatically when `Hidden layer 1 = 0`

#### Hidden 2 bias

What it does:
- enables a bias term on the second hidden-layer outgoing transition

What it affects:
- the source dimensions for the second-hidden outgoing matrix

What it represents:
- whether the second hidden layer contributes through a constant bias source

Special behavior:
- enabled only when `FeedForward` is selected and `Hidden layer 2 > 0`

### Weight range

Options:
- `-0.1 - 0.1`
- `-1 - 1`
- `-10 - 10`

What it does:
- sets the random initialization/reset range for weights

What it affects:
- how large or small new/reset weights can be
- the weight legend reference scale

What it represents:
- the numeric range used when weights are randomized

Important note:
- this is not a live clamp on existing weights
- it matters when weights are created or reset, not as a continuous training limiter

## 5.2 Training

This tab defines how training runs operate and exposes training history and rollback.

### Learning rate

Default options offered:
- `0.1`
- `0.2`
- `0.3`
- `0.4`
- `0.5`
- `0.8`
- `1.0`

The box is editable, so values outside the list can also be entered.

What it does:
- sets the size of the learning update step

What it affects:
- how aggressively weights change during training

What it represents:
- the training step scale

Practical interpretation:
- lower values: slower but usually steadier updates
- higher values: faster but potentially less stable updates

### Momentum

Default options offered:
- `0.0`
- `0.2`
- `0.5`
- `0.8`
- `0.9`

The box is editable.

What it does:
- carries part of the previous update into the next one

What it affects:
- how smoothly or aggressively the optimizer moves through weight space

What it represents:
- the persistence of recent training direction

Practical interpretation:
- `0.0`: no momentum
- higher values: stronger carry-over from previous updates

### Error threshold

What it does:
- sets the target error used for stopping logic

What it affects:
- when training can be considered “good enough” for the configured stop behavior

What it represents:
- the error level the run is trying to reach

### Learning steps

Default options offered:
- `100`
- `500`
- `1000`
- `5000`
- `10000`
- `50000`

The box is editable.

What it does:
- sets how many training steps one `Train #N` run performs

What it affects:
- how long a single training run lasts
- progress bar maximum
- the size of each saved training session block

What it represents:
- one user-visible training block size

### Training mode: `Batch update`

Unchecked:
- pattern/online updates

Checked:
- batch updates

What it does:
- switches between per-pattern updating and accumulated batch updating

What it affects:
- the update rule used during training

What it represents:
- whether weights are updated continuously or after aggregating multiple examples

### Error function: `Cross entropy`

Unchecked:
- sum-squared error

Checked:
- cross-entropy

What it does:
- changes the output-layer loss calculation

What it affects:
- training gradients
- displayed error behavior

What it represents:
- the cost function used for training

### `Reset`

What it does:
- reinitializes the active weights using the current definition and weight range

What it affects:
- destroys the current learned weight state
- clears current model progress

What it represents:
- start training again from fresh randomized weights

### `Train #N`

What it does:
- runs one more training block from the current weights

What it affects:
- active weights
- completed cycles
- error history
- training session history

What it represents:
- continue learning, not restart learning

Important note:
- `Train #2`, `Train #3`, and so on mean “continue from where the last run left off”

### Progress label

Examples:
- `Idle`
- active progress text
- completed-cycle text

What it does:
- reports the current training state

What it affects:
- nothing directly

What it represents:
- whether training is idle, in progress, or what history is currently restored

### Training progress bar

What it does:
- visualizes run progress during active training

What it affects:
- nothing directly

What it represents:
- how far the current training block has progressed

### Training sessions list

Each item summarizes:
- session number
- steps executed
- completed cycles
- displayed average error

What it does:
- exposes saved checkpoints created by training runs

What it affects:
- rollback target
- selected error history
- weight inspector default source linkage

What it represents:
- the visible history of your training milestones

### `Rollback`

What it does:
- restores the selected training session’s saved weights and history

What it affects:
- active weights
- displayed error history
- current run summary

What it represents:
- move the project back to a prior training checkpoint

## 5.3 Tests

This tab is the pattern-by-pattern behavior inspector.

### `Test selected`

What it does:
- evaluates only the currently selected pattern

What it affects:
- cached result for that pattern
- graph overlay for that selected pattern
- pattern inspector contents

What it represents:
- inspect one example in detail

### `Test all`

What it does:
- evaluates the full pattern set

What it affects:
- cached results for all patterns
- pass/fail markers
- test summary output

What it represents:
- inspect full dataset behavior for the current model

### Pattern list

Each row represents one pattern and can include:
- label
- last result summary
- pass/fail/not-tested indicator

What it does:
- selects which pattern is shown in the inspector

What it affects:
- pattern inspector contents
- network graph test overlay

What it represents:
- the navigable list of examples in the current dataset

### Pattern inspector title

What it does:
- identifies the current inspector area

What it affects:
- nothing

What it represents:
- that the right-hand panel is focused on the selected pattern

### Status text

What it does:
- summarizes the selected pattern’s current state

What it affects:
- nothing

What it represents:
- which pattern is selected and whether it has a cached result

### Result badge

Examples:
- `Not tested`
- `Pass`
- `Fail`

What it does:
- gives a quick outcome status for the selected pattern

What it affects:
- nothing directly

What it represents:
- the current cached classification-style outcome for that pattern

### Error text

Example:
- `Error: 0.123`

What it does:
- reports the selected pattern’s current error

What it affects:
- nothing directly

What it represents:
- numeric difference between expected and actual output for the selected example

### Meta panel

What it does:
- summarizes pattern metadata such as row counts and reset state

What it affects:
- nothing directly

What it represents:
- contextual information about the selected pattern

### Inputs panel

What it does:
- shows the selected pattern’s input vector

What it affects:
- nothing directly

What it represents:
- the actual inputs fed into the model

### Targets panel

What it does:
- shows the selected pattern’s target vector

What it affects:
- nothing directly

What it represents:
- the expected output for the pattern

### Outputs panel

What it does:
- shows the model’s current predicted outputs

What it affects:
- nothing directly

What it represents:
- what the model currently produces for the selected pattern

### Delta panel

What it does:
- shows output minus target for each comparable dimension

What it affects:
- nothing directly

What it represents:
- how far the prediction is from the target, dimension by dimension

### Hidden activations panel

What it does:
- shows the current hidden-unit activations for the selected pattern

What it affects:
- nothing directly

What it represents:
- the internal representation learned by the model for that example

### `Export selected`

What it does:
- exports the selected pattern’s hidden activations to CSV

What it affects:
- creates a file only

What it represents:
- a per-pattern hidden-state export

### `Export set`

What it does:
- exports hidden activations for the full current pattern set to CSV

What it affects:
- creates a file only

What it represents:
- dataset-wide hidden-state export for external analysis

### Inspector note

The note at the bottom reminds you:
- selecting a pattern updates the network graph with its latest cached test result

This is important because the graph and Tests tab are deliberately linked.

## 5.4 Patterns

This tab edits the project’s pattern set.

Patterns are stored inside the project file in Modern.

### Patterns introduction text

What it does:
- explains that patterns are stored in the project alongside settings and weights

What it affects:
- nothing directly

What it represents:
- the project-first Modern workflow

### Pattern editor status

What it does:
- reports parsing/sync state for the pattern editor

What it affects:
- nothing directly

What it represents:
- whether the current pattern text and graphic view are valid and synchronized

### Pattern mode tabs

Options:
- `Text mode`
- `Graphic mode`

What they do:
- switch between raw text editing and table editing

What they affect:
- the editing surface only

What they represent:
- two ways of editing the same underlying pattern data

### Text mode

What it does:
- edits the pattern set as raw text

What it affects:
- the live dataset if parsing succeeds
- the graphic table, which is refreshed from valid text

What it represents:
- direct textual control over the pattern file content

### Graphic mode

What it does:
- edits the pattern set through a structured table view

What it affects:
- the live dataset
- the raw text editor, which is rewritten to stay in sync

What it represents:
- a more guided editing view for pattern rows

### `Add pattern`

What it does:
- adds a new row to the pattern table

What it affects:
- the current pattern set
- the synchronized text representation

What it represents:
- append a new example to the dataset

### Graphic pattern rows

Each row can include:
- label
- reset marker
- input cells
- target cells
- delete action

#### Label

What it does:
- names the pattern

What it affects:
- list display
- inspector titles
- result labeling

What it represents:
- the human-readable identity of the example

#### Reset marker

What it does:
- marks whether recurrent context should be reset after that pattern

What it affects:
- SRN sequence behavior

What it represents:
- a sequence boundary in recurrent work

#### Input cells

What they do:
- store the input vector values for the row

What they affect:
- training/testing inputs

What they represent:
- the example’s input data

#### Target cells

What they do:
- store the target vector values for the row

What they affect:
- training targets
- pass/fail interpretation

What they represent:
- the expected output for the example

#### Delete action

What it does:
- removes the row

What it affects:
- the current dataset
- synchronized text

What it represents:
- remove a pattern from the project

## 5.5 Summary

This tab is the compact project and run overview.

### Latest run summary

What it does:
- reports the latest major run state

What it affects:
- nothing directly

What it represents:
- the most recent training/test/rollback summary

### Project summary

What it does:
- summarizes the active network definition and pattern set

What it affects:
- nothing directly

What it represents:
- a compact description of the current project

### Weights summary

What it does:
- summarizes the active weight state

What it affects:
- nothing directly

What it represents:
- a quick textual weight overview without opening the Weights tab

### Project state text

What it does:
- explains what will be saved in the project file

What it affects:
- nothing directly

What it represents:
- a reminder of Modern’s persistence model

## 6. Bottom panels

## 6.1 Console

The console is the structured activity log.

It supports Markdown-style rendering and export.

### `⧉` copy console

What it does:
- copies the full console content

What it affects:
- clipboard contents only

What it represents:
- quick export of the current session log

### `🖫` save console

What it does:
- saves the console as Markdown

What it affects:
- creates a `.md` file

What it represents:
- persist the current session log

### `🗑` clear console

What it does:
- clears the visible console content

What it affects:
- the console display only

What it represents:
- reset the current log view

### Console content area

What it does:
- shows structured log output for training, testing, rollback, export failures, and other actions

What it affects:
- nothing directly

What it represents:
- the narrative record of what you did in the project

## 6.2 Error graph

The error graph shows the training-error history for the current or selected training session.

### `🖫` save error graph

What it does:
- saves the error graph as PNG

What it affects:
- creates a file only

What it represents:
- export the current training-history chart

### Error graph canvas

What it does:
- plots training points from the current active history

What it affects:
- visual inspection only

What it represents:
- how error changed over the selected training run or restored session

Important behavior:
- selecting a training session can change which error history is shown

## 7. Common workflows

## 7.1 Create a new project

1. Click `✚ New`
2. Open the `Network` tab
3. Set:
- project name
- network type
- layer sizes
- biases
- weight range
4. Open `Patterns`
5. Enter pattern data
6. Open `Training`
7. Set training controls
8. Click `Reset` if needed
9. Click `Train #1`

## 7.2 Load an existing project

1. Click `📂 Load`
2. Select a `.swproj.json` file
3. Review:
- `Network`
- `Patterns`
- `Training sessions`
- `Summary`

## 7.3 Continue training

1. Open `Training`
2. Review or adjust:
- learning rate
- momentum
- error threshold
- learning steps
- batch update
- cross entropy
3. Click the next `Train #N`

Meaning:
- training continues from the current weights
- it does not restart from scratch

## 7.4 Roll back

1. Open `Training`
2. Select a checkpoint in `Training sessions`
3. Click `Rollback`
4. Review:
- `Summary`
- `Weights`
- `Error graph`

## 7.5 Test one pattern

1. Open `Tests`
2. Select a pattern
3. Click `Test selected`
4. Review:
- graph overlay
- outputs
- delta
- hidden activations

## 7.6 Test all patterns

1. Open `Tests`
2. Click `Test all`
3. Review:
- pass/fail markers
- pattern summaries
- selected pattern detail

## 7.7 Edit patterns

1. Open `Patterns`
2. Choose:
- `Text mode` for raw editing
- `Graphic mode` for row/cell editing
3. Confirm the status message remains valid

## 7.8 Compare weights across sessions

1. Open `Weights`
2. Set `Source` to `Current`
3. Review one or more layers
4. Change `Source` to a `Train #N` session
5. Compare stats and matrices

## 7.9 Run analysis

1. Open `Analysis`
2. Set `Mode`
3. Set `Source`
4. If needed, set chart-specific selectors
5. Click `Run`
6. Use copy/save/export buttons as needed

## 8. What gets saved in a Modern project

Modern project files store:

- network definition
- pattern set
- current weights
- completed cycles
- workspace state
- training sessions
- session-specific error histories

The current project schema in code is:
- `signalweave-project/v4`

Backward compatibility:
- legacy `v1`, `v2`, and `v3` project formats still load

What this means in practice:
- loading a project can restore not just topology and weights, but also rollback-ready training history

## 9. How to interpret the app

### When to use Network Graph

Use it when you want:
- a topology overview
- quick visual weight sign/magnitude understanding
- activation overlays for the selected tested pattern

### When to use Weights

Use it when you want:
- exact matrix inspection
- checkpoint comparisons
- numeric weight statistics

### When to use Tests

Use it when you want:
- exact per-pattern behavior
- pass/fail scanning
- target/output comparison
- hidden activation inspection

### When to use Analysis

Use it when you want:
- dataset-level structure
- clustering
- signal evolution across patterns
- surface behavior across two inputs

### When to use Summary

Use it when you want:
- a compact textual snapshot of the current project and current state

## 10. Troubleshooting notes

### The graph changed after I selected a pattern

This is expected.

The Tests tab and Network Graph are connected:
- selecting a pattern updates the graph to show that pattern’s cached result

### The second hidden layer is disabled

This is expected when:
- `Network type` is `SimpleRecurrent`
- or `Hidden layer 1` is `0`

### A bias checkbox disabled itself

This is expected when its corresponding layer size is `0` or not allowed by the current network type.

### The Weights or Analysis source list contains `Train #N`

That means the application has saved user-visible training checkpoints and you can inspect historical states directly.

### `Train #N` looks odd at first

It is intentionally explicit.

It means:
- “run one more training block”
- not “start a different mode”
- not “restart training”

### Pattern editing in one mode changed the other mode

This is expected.

`Text mode` and `Graphic mode` are two views over the same live pattern set.

## Related documents

- [modern-ui-guide.md](/home/simon/signalweave/docs/modern-ui-guide.md)
- [signalweave-schema.md](/home/simon/signalweave/docs/signalweave-schema.md)
- [product-lines.md](/home/simon/signalweave/docs/product-lines.md)
