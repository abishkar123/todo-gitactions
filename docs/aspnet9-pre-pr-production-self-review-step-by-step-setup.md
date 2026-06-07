# ASP.NET 9 Pre-PR Production Self Review Step-by-Step Setup

This file is a step-by-step setup guide for a GitHub Agentic Workflows prompt that performs a production-sensitive ASP.NET 9 pre-PR self-review.

It is documentation only. It does not define a runnable workflow.

## 1. Create the workflow prompt file

Create a new workflow markdown file under `.github/workflows/`.

Use a descriptive name such as:

- `aspnet9-pre-pr-production-self-review.md`

## 2. Add workflow frontmatter

Add the standard GitHub Agentic Workflows frontmatter:

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

Recommended event triggers:

- `pull_request` with `opened`, `synchronize`, `reopened`, and `ready_for_review`
- `push` to `development`
- `workflow_dispatch` with an optional `target_branch`

Recommended permissions:

- `contents: read`
- `issues: read`
- `pull-requests: read`

## 3. Define the review purpose

The workflow prompt should say that the review is:

- a pre-submission self-review
- focused on production-sensitive ASP.NET 9 changes
- not a final reviewer approval
- intended to catch issues before the PR is submitted

## 4. Set the review assumptions

State these assumptions explicitly in the prompt:

- default assumption: no functional change unless declared
- prioritize production stability
- prioritize zero behavioral regression
- prioritize security, rollback safety, and controlled modernization

## 5. List the review goals

Tell the agent to detect:

1. Hidden logic changes or behavior changes
2. Production stability risks
3. Security concerns
4. Startup, deployment, or runtime failure risks
5. Request pipeline, auth/authz, config, DI, serialization, mapping, validation, null/default, ordering, or side-effect changes
6. Legacy carryover or migration anti-patterns
7. Async, blocking, thread-safety, static/shared state, or exception-handling risks
8. Dependency or package supportability issues
9. Rollback risk
10. PR scope that is too broad for safe review

## 6. Define the findings format

Require the agent to return exactly one markdown report with these sections:

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

## 7. Add inline review comment support

Add a `review_comments` JSON block so the workflow can post GitHub-style inline comments when exact file and line evidence exists.

Each comment should include:

- `path`
- `line`
- `side`
- `severity`
- `category`
- `classification`
- `body`

Use `side: RIGHT` unless the comment refers to removed code.

## 8. Give the agent direct instructions

Tell the agent to:

- fetch PR metadata and changed files
- inspect the diff and changed files
- classify findings by severity and category
- produce the markdown report
- emit inline review comments when line evidence is exact
- avoid approving, merging, or modifying code
- keep GitHub operations read-only except for safe outputs

## 9. Add the execution flow

The prompt should include a clear execution flow:

1. Determine event type
2. Determine comparison target branch
3. Fetch diff and changed files
4. Focus on production-impacting files
5. Analyze behavior change and production risk
6. Classify findings
7. Generate the report
8. Emit `review_comments` when appropriate
9. Post review output for PR context

## 10. Add GitHub code review behavior

Explain that the workflow should behave like GitHub code review by:

- preserving exact file and line evidence
- converting eligible findings into inline comments
- keeping inline comments short and actionable
- skipping inline comments when line evidence is not exact

## 11. Add validation guidance

Tell the user to validate the workflow with:

- `gh aw validate <workflow>.md`
- `gh aw compile <workflow>.md`
- parsing the generated `.lock.yml`

## 12. Keep the prompt focused

Do not add cosmetic review guidance unless it affects production risk, hidden behavior change, supportability, or future upgrade friction.

## 13. Recommended prompt skeleton

Use this as the body of the workflow prompt:

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

## Output Format

Return exactly one Markdown report with:

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

