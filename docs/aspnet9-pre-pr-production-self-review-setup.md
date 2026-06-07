# ASP.NET 9 Pre-PR Production Self Review Setup

This document describes how to set up a custom GitHub Agentic Workflows prompt for a production-sensitive ASP.NET 9 pre-PR self-review.

It is documentation only. It does not define a runnable workflow.

## Goal

Review the current branch diff against its target branch before a PR is submitted.

The review should behave like GitHub code review:

- produce a markdown report
- identify production-risk findings
- emit inline review comments when a finding has exact file and line evidence

## When to Use

Use this setup for:

- ASP.NET 9 migrations
- production-sensitive refactors
- controller, service, middleware, DI, auth, config, validation, serialization, data-access, or startup changes
- PRs that may contain hidden behavior changes

## What This Review Must Detect

1. Hidden logic or behavior changes
2. Production stability risks
3. Security concerns
4. Startup, deployment, or runtime failure risks
5. Request pipeline, auth/authz, config, DI, serialization, mapping, validation, null/default, ordering, or side-effect changes
6. Legacy carryover or migration anti-patterns
7. Async, blocking, thread-safety, static/shared state, or exception-handling risks
8. Dependency or package supportability issues
9. Rollback risk
10. PR scope that is too broad for safe review

## Required Output Sections

The agent should return exactly one markdown report with:

1. Submission decision
2. Executive summary
3. Blockers before PR submission
4. Findings table
5. Hidden behavior change warnings
6. Legacy carryover concerns
7. Missing validation evidence
8. Rollback concerns
9. Scope reduction suggestions
10. Suggested PR declaration

## Inline Review Comment Rules

When a finding has exact file and line evidence, the agent should emit a machine-readable JSON block named `review_comments`.

Each inline review comment should include:

- `path`
- `line`
- `side` (`RIGHT` unless the comment refers to removed code)
- `severity`
- `category`
- `classification`
- `body`

### Example

```json
{
  "review_comments": [
    {
      "path": "Controllers/TasksController.cs",
      "line": 84,
      "side": "RIGHT",
      "severity": "High",
      "category": "stability",
      "classification": "immediate remediation required",
      "body": "This change alters the failure path without calling out the behavior change. Keep the previous guard or document the new runtime behavior."
    }
  ]
}
```

## Step-by-Step Setup

1. Create a new agentic workflow prompt in `.github/workflows/`.
2. Set the workflow metadata:
   - `name`
   - `description`
   - `on`
   - `permissions`
   - `strict: true`
   - `engine: copilot`
   - `run-name`
   - `concurrency`
   - `timeout-minutes`
   - `tools`
   - `safe-outputs`
3. Add a frontmatter body that instructs the model to:
   - compare the current branch to the target branch
   - inspect the PR diff and changed files
   - focus on production-impacting ASP.NET 9 changes
   - classify findings by severity and category
   - generate a markdown report
   - emit `review_comments` when exact line evidence exists
4. Include a `Step-by-Step Setup` section in the prompt so the model knows how to inspect the diff.
5. Include a `Review Goals` section so the review scope is precise.
6. Include a `Findings Rules` section so the output is structured and comparable.
7. Add the `Inline Review Comments Payload` section to make GitHub-style inline comments possible.
8. Add `Agent Instructions` that tell the model not to approve, merge, or modify code.
9. If you want the workflow to post GitHub code review comments, add a post-processing step in the generated lock file that:
   - reads the report output
   - extracts the `review_comments` JSON
   - calls `pulls.createReview`
   - writes the report to an artifact or file
10. Validate the workflow with `gh aw validate <workflow>.md`.
11. Compile it with `gh aw compile <workflow>.md`.
12. Confirm the generated `.lock.yml` parses cleanly.

## Recommended Prompt Shape

Use this structure in the workflow markdown body:

```markdown
# ASP.NET 9 Pre-PR Production Self Review

## Context

- This is a pre-submission review, not a final reviewer review.
- Default assumption: no functional change unless explicitly declared.
- Priorities: production stability, zero behavioral regression, security, rollback safety, and controlled modernization.
- Focus on issues that should be fixed or declared before the PR is submitted.

## Review Goals

1. Detect hidden logic changes or behavior changes.
2. Detect production stability risks.
3. Detect security concerns.
4. Detect startup, deployment, or runtime failure risks.
5. Detect request pipeline, auth/authz, config, DI, serialization, mapping, validation, null/default, ordering, or side-effect changes.
6. Detect legacy carryover or migration anti-patterns that should not remain.
7. Detect async, blocking, thread-safety, static/shared state, or exception-handling risks.
8. Detect dependency or package supportability issues.
9. Detect rollback risk.
10. Detect whether the PR scope is too broad and should be split.

## Findings Rules

- Do not focus on cosmetic style unless it creates production risk, hidden behavior change, supportability risk, or future upgrade friction.
- For each issue found, provide severity, category, classification, evidence, impact, behavior change analysis, recommended fix, validation, and rollback concern.

## Output Format

Return exactly one Markdown report with these sections:

1. Submission decision
2. Executive summary
3. Blockers before PR submission
4. Findings table
5. Hidden behavior change warnings
6. Legacy carryover concerns
7. Missing validation evidence
8. Rollback concerns
9. Scope reduction suggestions
10. Suggested PR declaration
```

## GitHub Review Comment Behavior

To make the workflow behave like GitHub code review:

- preserve exact file/line evidence in findings
- convert those findings into inline comments
- prefer `side: RIGHT`
- only comment when the diff line is known
- keep inline comments short and actionable

## Practical Notes

- If the review has no exact line evidence, output the markdown report only.
- If the change is broad, call out the need to split the PR.
- If behavior changes are intentional, require the PR description to declare them.

