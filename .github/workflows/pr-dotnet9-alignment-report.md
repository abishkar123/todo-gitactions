---
name: PR .NET 9 Alignment Report
description: Review each pull request and report whether the code changes stay aligned with .NET 9.
on:
  pull_request:
    types: [opened, synchronize, reopened, ready_for_review]
permissions:
  contents: read
  pull-requests: read
  issues: read
  actions: read
strict: true
engine: copilot
run-name: ".NET 9 alignment report for PR #${{ github.event.pull_request.number }}"
concurrency:
  group: dotnet9-alignment-${{ github.event.pull_request.number }}
  cancel-in-progress: true
timeout-minutes: 20
network:
  allowed:
    - defaults
    - dotnet
tools:
  github:
    toolsets: [default, search]
  bash: true
safe-outputs:
  add-comment:
    max: 1
    footer: false
steps:
  - name: Setup .NET 9 SDK
    uses: actions/setup-dotnet@v4
    with:
      dotnet-version: 9.0.x
---
# PR .NET 9 Alignment Reporter

Review pull request `${{ github.event.pull_request.number }}` in `${{ github.repository }}` and post one concise report comment that answers whether the PR remains aligned with .NET 9.

## Goal

Determine whether the pull request keeps the repository consistent with .NET 9 across source code, project configuration, package references, and CI or tooling changes.

## Constraints

- Keep the agent job read-only. Any GitHub write must happen only through `safe-outputs`.
- Work only from the triggering pull request context.
- Be precise and evidence-based. Do not make claims you cannot support from the diff, repository files, or command output.
- If the pull request is a draft, still report what you can, but clearly say the review is preliminary.

## Required Investigation

1. Fetch the pull request metadata and changed files with the GitHub tools.
2. Inspect any changed files that could affect .NET 9 compatibility, including:
   - `*.csproj`
   - `*.sln` or `*.slnx`
   - `global.json`
   - `.github/workflows/*`
   - `Dockerfile*`
   - source files that introduce framework-specific APIs or package usage
3. Check the current repository baseline for .NET targeting and existing CI expectations.
4. Run the local verification commands when the PR touches code, projects, packages, or build configuration:
   - `dotnet --info`
   - `dotnet restore TodoList.sln`
   - `dotnet format TodoList.sln --verify-no-changes --no-restore`
   - `dotnet build TodoList.sln --configuration Release --no-restore`
   - `dotnet test TodoList.Tests/TodoList.Tests.csproj --configuration Release --no-build`
5. If the PR only changes docs or other non-runtime assets, skip build and test commands and say why.

## Alignment Rules

Treat the pull request as `.NET 9 aligned` when all of the following are true:

- Project target frameworks remain on `net9.0`, or changes do not affect framework targeting.
- Build and test setup continues to use .NET 9 where relevant.
- Formatting and analyzer checks enforced by `dotnet format` still pass when relevant.
- Package or tooling changes do not obviously downgrade the repository away from .NET 9 support.
- The diff does not introduce code that is incompatible with the repository's .NET 9 baseline.
- Validation commands pass when they are relevant.

Treat the pull request as `not aligned with .NET 9` when any of the following are true:

- A project or workflow downgrades the target framework or SDK setup away from .NET 9.
- The PR causes `dotnet format --verify-no-changes` to fail on relevant code or project changes.
- A package or tooling change is incompatible with the existing .NET 9 baseline.
- The PR introduces code changes that are likely to break on the current .NET 9 target.
- Required validation commands fail because of the PR changes.

Treat the pull request as `needs follow-up` when the evidence is mixed or incomplete. In that case, explain exactly what is uncertain.

## Comment Format

Post exactly one Markdown comment with this structure:

`## .NET 9 Alignment Report`

- `Verdict:` `Aligned`, `Not aligned`, or `Needs follow-up`
- `Summary:` one short paragraph
- `Evidence:` 2-5 bullets with the most important findings
- `Validation:` include the commands you ran and whether they passed, or state that validation was skipped with a reason
- `Follow-up:` `None` or a short actionable list

Keep the tone direct and technical. Mention concrete files when they matter.
