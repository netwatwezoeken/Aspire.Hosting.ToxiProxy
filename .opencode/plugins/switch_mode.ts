/**
 * switch_mode plugin
 *
 * Manages the four-phase development workflow:
 *   specs → plan → build → pr
 *
 * Uses the same mechanism as the built-in plan_exit tool:
 *   session.prompt with noReply:true + synthetic:true stores a user message
 *   with the target agent's name.  The session loop that is already running
 *   reads the new message on its next iteration and automatically routes to
 *   the correct agent, updating the TUI agent indicator as a side-effect.
 */

import { tool } from "@opencode-ai/plugin"
import type { Plugin } from "@opencode-ai/plugin"

// ── Workflow definition ──────────────────────────────────────────────────────

const WORKFLOW = ["specs", "plan", "build", "pr"] as const
type WorkflowMode = (typeof WORKFLOW)[number]

/** Instruction given to the incoming agent as the first line of its handoff. */
const TRANSITION_INSTRUCTION: Record<string, string> = {
  specs:
    "The specifications are complete and have passed the consistency gate. " +
    "Create a detailed, vertical-slice implementation plan together with " +
    "per-slice Gherkin scenarios. Do not write any implementation code yet.",

  plan:
    "The plan has been approved. Implement it slice by slice, following the " +
    "Gherkin scenarios as the acceptance tests. Edit files as needed.",

  build:
    "The implementation is complete. Create a pull request: write a clear " +
    "title, a description that references the spec and plan, and a summary " +
    "of every file changed.",
}

// ── Plugin ───────────────────────────────────────────────────────────────────

export const SwitchModePlugin: Plugin = async ({ client }) => {
  return {
    tool: {
      switch_mode: tool({
        description: `Transition the session to the next phase in the development workflow.

Workflow order (fixed):  specs → plan → build → pr

  specs  – Collaborative requirements & specification writing
  plan   – Vertical-slice planning + Gherkin scenario authoring (read-only)
  build  – Implementation (full file access)
  pr     – Pull-request creation

Call this tool when the current phase is done and the user has confirmed they
want to proceed.  Always confirm with the user before calling.

The tool stores a synthetic handoff message as a new user message addressed to
the next agent.  The session loop picks it up immediately and switches agents
(same mechanism as the built-in plan_exit tool).  You do not need to do
anything else – the next agent will start on its own.`,

        args: {
          summary: tool.schema.string().describe(
            "Concise summary of what was accomplished in the current phase.  " +
              "This is forwarded verbatim to the next agent as handoff context.",
          ),
        },

        async execute(args, context) {
          // ── 1. Validate current position in the workflow ─────────────────
          const currentAgent = context.agent
          const currentIdx = WORKFLOW.indexOf(currentAgent as WorkflowMode)

          if (currentIdx === -1) {
            return (
              `Agent "${currentAgent}" is not part of the managed workflow ` +
              `(${WORKFLOW.join(" → ")}).  No transition performed.`
            )
          }

          if (currentIdx === WORKFLOW.length - 1) {
            return `Already at the final phase "${currentAgent}" (pr).  The workflow is complete.`
          }

          // ── 2. Build the handoff message ─────────────────────────────────
          const nextMode = WORKFLOW[currentIdx + 1]
          const instruction = TRANSITION_INSTRUCTION[currentAgent]

          const handoffText =
            `${instruction}\n\n` +
            `## Handoff from ${currentAgent} phase\n\n` +
            `${args.summary}`

          // ── 3. Store a synthetic user message addressed to the next agent ─
          //
          // noReply: true  → stores the message without starting a second loop.
          // synthetic: true → the message is hidden from the user's chat view
          //                   but visible to the AI (same as plan_exit).
          //
          // The still-running session loop reads this message on the next
          // iteration, sees agent: nextMode, and continues as that agent.
          // session.prompt also calls setAgentModel internally, so the TUI
          // indicator updates automatically.
          await client.session.prompt({
            path: { id: context.sessionID },
            body: {
              agent: nextMode,
              noReply: true,
              parts: [
                {
                  type: "text" as const,
                  text: handoffText,
                  synthetic: true,
                },
              ],
            },
          })

          return (
            `Transition queued: **${currentAgent} → ${nextMode}**.\n\n` +
            `The ${nextMode} agent will take over on the next turn with the ` +
            `handoff context.  You can stop here.`
          )
        },
      }),
    },
  }
}

export default SwitchModePlugin
