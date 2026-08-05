This repository-level agent work log records every substantive change or investigation performed by the autonomous agent(s).

Purpose
- Provide a machine- and human-readable record of what the agent did, why, and where to find the changes.
- Support reproducibility and audit for the MSc dissertation engineering tool.

Primary log file
- agent_work_log.json (root)

Schema (agent_work_log.json entries array)
- id: string (unique, e.g., step-20260805-001)
- timestamp: ISO-8601 string
- author: string (agent id, e.g., "GitHub Copilot")
- summary: short description of the change/action
- rationale: why the action was taken
- filesChanged: array of file paths modified or created
- commandsRun: array of terminal commands run (if any)
- testsRun: array of test identifiers and results (if any)
- buildResult: brief build status (if applicable)
- references: array of relevant file paths (e.g., the context brief)
- notes: freeform observations

How the agent will use this file
- Append a new entry object to the entries array for each future work step.
- Keep entries concise and factual.
- Update buildResult and testsRun when relevant.

Example entry
{
  "id": "step-20260805-001",
  "timestamp": "2026-08-05T00:00:00Z",
  "author": "GitHub Copilot",
  "summary": "Read Undercut_Tool_Agent_Context.md and created logging artifacts",
  "rationale": "User requested the agent to record future work in a parseable document",
  "filesChanged": ["agent_work_log.json", "AGENT_WORK_LOG_README.md"],
  "commandsRun": [],
  "testsRun": [],
  "buildResult": null,
  "references": ["new-code/UndercutAnalyser/Undercut_Tool_Agent_Context.md"],
  "notes": "Initial log created. Agent will append future entries."
}

Addendum
- Humans may edit the README to change the schema version; the agent will preserve existing entries.
- If the repository already has a different agent log, notify maintainers before duplicating.
