# IntentInk Product Development Phase

## Product focus

IntentInk is being shaped around a clear and practical use case: helping developers write better engineering communication without leaving their normal workflow.

The core value is not generic grammar correction. The core value is faster and higher-quality communication for:

- commit messages
- Jira ticket titles and descriptions
- pull request titles and descriptions
- release notes and changelog summaries
- internal engineering updates

This makes the product useful in the exact moments where developers already write text under time pressure.

---

## Why this direction fits the product

The current architecture already aligns well with this direction:

- it runs in the background,
- it responds to selected text,
- it works across apps,
- it stays local and private,
- it is lightweight and non-intrusive,
- it can insert generated text directly back into the source application.

This is a strong fit for writing tasks inside GitHub, Jira, VS Code, Slack, and browser-based tools.

---

## Primary user

The primary user is a software developer or engineering team member who writes:

- technical updates,
- commit summaries,
- issue descriptions,
- PR explanations,
- release notes,
- and project updates.

This user values:

- speed,
- clarity,
- technical accuracy,
- concise communication,
- consistency in writing style,
- and minimal disruption to their workflow.

---

## Product goal

The goal is to transform IntentInk from a general grammar helper into a focused developer writing assistant.

The product should help users turn rough notes into polished engineering communication in just a few clicks.

Examples:

- rough bug note -> Jira ticket description
- code changes -> commit message
- diff notes -> PR summary
- feature updates -> release note draft

---

## Core workflows

### 1. Commit message generation

The user selects a rough description or uses a generated summary from a diff.

IntentInk generates:

- conventional commit title
- optional body with summary and impact
- concise professional wording

Example:

Input:
"fix login issue with timeout and retry not working when API fails"

Output:
"fix(auth): handle retry timeout failures during API outage recovery"

---

### 2. Jira ticket writing

The user pastes notes or a rough requirement.

IntentInk generates:

- ticket title
- problem statement
- expected behavior
- acceptance criteria
- root cause summary

This helps engineering teams create cleaner tickets faster.

---

### 3. PR title and description generation

The user selects commit messages, issue notes, or a rough summary.

IntentInk generates:

- PR title
- summary of changes
- why the change was needed
- testing performed
- potential risks or follow-ups

This reduces the manual effort of drafting reviews and release communication.

---

### 4. Release note generation

From merged tickets or PRs, IntentInk can help summarize:

- new features
- fixes
- improvements
- breaking changes
- migration notes

This is especially useful for product or engineering teams releasing software on a schedule.

---

## Feature direction

### Phase 1: developer communication basics

Focus on the highest-value writing tasks:

- commit message generation
- PR title and description generation
- Jira ticket title and description generation
- tone selection
- concise / technical / formal modes
- copy-to-clipboard and inline insert

### Phase 2: workflow polish

Add features that improve adoption:

- templates for common engineering tasks
- conventional commit validation
- ticket template presets
- saved user writing preferences
- preferred tone profiles
- project-specific vocabulary

### Phase 3: team collaboration support

Expand from individual writing help into team workflows:

- release note generation
- changelog summaries
- issue status summaries
- sprint recap drafts
- engineering update templates

### Phase 4: extensibility and ecosystem

Open the product for broader developer workflows:

- app-specific templates
- custom prompt profiles
- plugin-like writing actions
- internal company terminology dictionaries
