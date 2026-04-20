---
name: .NET Dependency Compliance Report
description: Audit .NET packages and project dependency health on a daily schedule and report actionable upgrade and security findings.
on:
  schedule: daily on weekdays
permissions:
  contents: read
  issues: read
  pull-requests: read
strict: true
engine: copilot
run-name: ".NET dependency compliance report"
concurrency:
  group: dotnet-dependency-compliance-report
  cancel-in-progress: true
timeout-minutes: 30
network:
  allowed:
    - defaults
    - dotnet
tools:
  github:
    toolsets: [default, search]
  bash: true
safe-outputs:
  create-issue:
    max: 1
    footer: false
  add-comment:
    max: 1
    footer: false
steps:
  - name: Setup .NET 9
    uses: actions/setup-dotnet@v4
    with:
      dotnet-version: 9.0.x
---
# .NET Dependency Compliance Reporter

Inspect all .NET package and project dependencies used by this repository and produce a structured dependency compliance report for engineering planning.

## Goal

Find actionable dependency maintenance work across the repository by checking for:

- newly released package versions
- known security vulnerabilities
- deprecated or unsupported packages
- project dependency or target-framework risks that affect upgrade safety

Do not apply package upgrades, edit project files, or open pull requests with code changes.

## Required Scope

Audit the full repository, not just one project. Inspect at minimum:

- solution files: `*.sln`, `*.slnx`
- project files: `*.csproj`, `*.fsproj`, `*.vbproj`
- central package files: `Directory.Packages.props`, `Directory.Build.props`, `Directory.Build.targets`
- SDK and restore configuration: `global.json`, `NuGet.config`, `nuget.config`, `packages.lock.json`
- project-to-project references and target frameworks

If some of these files do not exist, say so briefly in the report.

## Required Investigation

1. Enumerate the repository's .NET projects, solution files, direct `PackageReference` items, and `ProjectReference` items.
2. Run the local validation and inventory commands from the repository root:
   - `dotnet --info`
   - `dotnet restore TodoList.sln`
   - `dotnet list TodoList.sln package --include-transitive --format json`
   - `dotnet list TodoList.sln package --outdated --include-transitive --format json`
   - `dotnet list TodoList.sln package --vulnerable --include-transitive --format json`
   - `dotnet list TodoList.sln package --deprecated --include-transitive --format json`
3. If the solution file name changes or multiple solutions exist, detect them and audit each relevant solution instead of assuming `TodoList.sln`.
4. Inspect project files directly so the report includes context about current target frameworks, SDK style, and direct package ownership.
5. When any update, vulnerability, or deprecation finding exists, review the official release sources before writing conclusions:
   - `https://github.com/dotnet/core/blob/main/release-notes/README.md`
   - `https://github.com/dotnet/sdk/releases`
6. Use the GitHub tools and/or `curl` to gather evidence from those official sources. Prefer the sources most relevant to the detected version jump or SDK/runtime impact.

## Analysis Rules

- Distinguish direct dependencies from transitive dependencies.
- Treat security findings as highest priority.
- Call out packages that are Microsoft or ASP.NET related separately when framework or SDK alignment matters.
- If a package has a newer patch version within the same major/minor line, classify it as a likely non-breaking upgrade unless the evidence says otherwise.
- If a package change crosses a major version, or if the release sources indicate breaking changes, classify it as remediation required.
- If the repository target framework, SDK expectations, or test/build workflow create friction for an upgrade, explain that friction explicitly.
- If there are no actionable findings, state that clearly and do not create or update a GitHub issue.

## Impact Assessment

For each actionable dependency finding, assess:

- current version
- recommended version or version band
- direct or transitive usage
- security or support concern
- likely upgrade class: `Non-breaking`, `Low-risk with validation`, or `Requires remediation`
- expected impact on this repository specifically

Repository-specific impact should consider at least:

- current target frameworks
- test coverage available in this repository
- whether the package is runtime-critical, test-only, or infrastructure-only
- whether project references or application startup code are likely to be affected

## Safe Upgrade Guidance

Recommend a safe upgrade path that keeps the solution current without making changes automatically. Prefer advice such as:

- upgrade patch versions first
- isolate major-version changes into separate pull requests
- validate build and tests after each step
- upgrade Microsoft and ASP.NET packages in a coordinated way when framework alignment matters

When official release notes do not provide enough evidence for a strong conclusion, say so and keep the recommendation conservative.

## Report Format

Produce exactly one Markdown report using this structure:

`## .NET Dependency Compliance Report`

- `Date:` current run date in UTC
- `Scope:` short description of what was scanned
- `Status:` `No action needed`, `Updates available`, `Security action required`, or `Remediation planning required`
- `Summary:` one short paragraph
- `Current Baseline:` 2-5 bullets covering target framework, solution/package structure, and notable dependency patterns
- `Findings:` a table with columns `Dependency`, `Current`, `Recommended`, `Type`, `Risk`, `Impact`, `Notes`
- `Breaking Changes And Upgrade Risks:` 1-5 bullets, or `None`
- `Recommended Upgrade Path:` 1 numbered list with a conservative step-by-step plan
- `Validation Evidence:` bullets listing the commands run and key outcomes
- `Sources Reviewed:` bullets with the official Microsoft/.NET sources consulted

If there are no actionable findings, keep the `Findings` table empty except for a single row stating that no updates, vulnerabilities, or deprecations requiring action were detected.

## GitHub Output Rules

- If the report contains actionable findings, search for an open issue in this repository titled `.NET Dependency Compliance Report`.
- If that issue exists, add exactly one comment containing the full report.
- If it does not exist, create exactly one issue with title `.NET Dependency Compliance Report` and use the full report as the issue body.
- If there are no actionable findings, do not create an issue or comment. End cleanly after producing the report in the workflow output.

## Constraints

- Keep the agent job read-only. Any GitHub write must go only through `safe-outputs`.
- Do not suggest automatic package bump commits.
- Do not change package versions, SDK configuration, workflow files, or source code.
- Be precise. Every risk claim must be supported by local repository evidence, command output, or the official sources listed above.
