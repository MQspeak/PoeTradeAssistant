# Flow Editor First Slice Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first runnable visual flow-editor slice with persisted flow graphs, basic runtime rules, a new WPF flow-editor tab, and execution support for click, OCR, and text-input nodes.

**Architecture:** Extend `AppProfile` with persisted flow graph models in `Poe2MarketScanner.Core`, normalize and validate them on load, and expose editor-friendly view models from `MainViewModel`. Render the editor inside a new WPF tab backed by a `Canvas` plus `ItemsControl`, while keeping execution logic in services and reusing existing anchors, OCR regions, and automation primitives.

**Tech Stack:** .NET 6, WPF, xUnit, System.Text.Json

---

### Task 1: Add core flow-model persistence coverage

**Files:**
- Modify: `tests/Poe2MarketScanner.Core.Tests/ProfileStorageTests.cs`
- Create: `tests/Poe2MarketScanner.Core.Tests/FlowRuntimePlanTests.cs`
- Test: `tests/Poe2MarketScanner.Core.Tests/ProfileStorageTests.cs`
- Test: `tests/Poe2MarketScanner.Core.Tests/FlowRuntimePlanTests.cs`

- [ ] **Step 1: Write failing persistence tests for `flows` round-trip**
- [ ] **Step 2: Run the targeted core tests and confirm the new cases fail for missing flow support**
- [ ] **Step 3: Write failing runtime-plan tests for reachable nodes, ordered condition routing, and OCR-dependent conditions**
- [ ] **Step 4: Run the new runtime tests and confirm they fail for missing flow runtime support**

### Task 2: Implement core flow models, normalization, and runtime planning

**Files:**
- Modify: `src/Poe2MarketScanner.Core/Configuration/AppProfile.cs`
- Modify: `src/Poe2MarketScanner.Core/Configuration/AppProfileFactory.cs`
- Modify: `src/Poe2MarketScanner.Core/Configuration/AppProfileNormalizer.cs`
- Modify: `src/Poe2MarketScanner.Core/Configuration/JsonProfileStorageService.cs`
- Create: `src/Poe2MarketScanner.Core/Configuration/FlowDefinition.cs`
- Create: `src/Poe2MarketScanner.Core/Configuration/FlowGraphRuntime.cs`
- Test: `tests/Poe2MarketScanner.Core.Tests/ProfileStorageTests.cs`
- Test: `tests/Poe2MarketScanner.Core.Tests/FlowRuntimePlanTests.cs`

- [ ] **Step 1: Add minimal flow graph model types and wire `Flows` into `AppProfile`**
- [ ] **Step 2: Normalize missing or partial flow data, ensuring a single start node per flow**
- [ ] **Step 3: Persist `flows` in JSON load/save while preserving backward compatibility**
- [ ] **Step 4: Implement runtime helpers for reachable nodes, ordered edge selection, and OCR condition evaluation**
- [ ] **Step 5: Re-run the targeted core tests until they pass**

### Task 3: Add app-level flow-editor state coverage

**Files:**
- Modify: `tests/Poe2MarketScanner.App.Tests/MainViewModelTests.cs`
- Modify: `tests/Poe2MarketScanner.App.Tests/AppResourceTests.cs`
- Test: `tests/Poe2MarketScanner.App.Tests/MainViewModelTests.cs`
- Test: `tests/Poe2MarketScanner.App.Tests/AppResourceTests.cs`

- [ ] **Step 1: Write failing `MainViewModel` tests for multiple flows, node creation, and persistence through save**
- [ ] **Step 2: Run the `MainViewModel` tests and confirm they fail because the editor state is not exposed yet**
- [ ] **Step 3: Write failing resource tests for the new flow-editor tab and text-input node controls**
- [ ] **Step 4: Run the resource tests and confirm they fail because the XAML is not updated yet**

### Task 4: Implement flow-editor view models and main-window UI

**Files:**
- Modify: `src/Poe2MarketScanner.App/MainViewModel.cs`
- Modify: `src/Poe2MarketScanner.App/MainWindow.xaml`
- Create: `src/Poe2MarketScanner.App/FlowEditorViewModels.cs`
- Test: `tests/Poe2MarketScanner.App.Tests/MainViewModelTests.cs`
- Test: `tests/Poe2MarketScanner.App.Tests/AppResourceTests.cs`

- [ ] **Step 1: Add flow-editor view models for flows, nodes, connections, and selection state**
- [ ] **Step 2: Expose flow collections and commands from `MainViewModel`, reusing anchors and OCR regions as selector sources**
- [ ] **Step 3: Add the new `流程编辑` tab with flow list, toolbar, canvas, and property panel**
- [ ] **Step 4: Re-run app view-model and resource tests until they pass**

### Task 5: Extend automation primitives for flow execution

**Files:**
- Modify: `src/Poe2MarketScanner.App/Services/IInputAutomationRunner.cs`
- Modify: `src/Poe2MarketScanner.App/Services/WindowsInputAutomationRunner.cs`
- Create: `src/Poe2MarketScanner.App/Services/FlowAutomationRunner.cs`
- Create: `src/Poe2MarketScanner.App/Services/FlowExecutionModels.cs`
- Create: `tests/Poe2MarketScanner.App.Tests/FlowAutomationRunnerTests.cs`
- Test: `tests/Poe2MarketScanner.App.Tests/FlowAutomationRunnerTests.cs`

- [ ] **Step 1: Write failing flow-runner tests for modifier-click, text input clearing, and start-reachable execution**
- [ ] **Step 2: Run the new flow-runner tests and confirm they fail for missing services**
- [ ] **Step 3: Expand the input runner interface to support modifier-key click combinations**
- [ ] **Step 4: Implement the minimal flow runner that executes click, OCR, and text-input nodes with edge routing**
- [ ] **Step 5: Re-run the flow-runner tests until they pass**

### Task 6: Verify the first runnable slice end-to-end

**Files:**
- Verify only

- [ ] **Step 1: Run the focused core test suite**
- [ ] **Step 2: Run the focused app test suite**
- [ ] **Step 3: Run a build or broader test command if the focused suites are green**
- [ ] **Step 4: Compare the implemented behavior against the spec and note any intentional first-slice gaps**
