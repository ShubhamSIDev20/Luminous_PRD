import json
import sys

try:
    payload = json.load(sys.stdin)
except Exception:
    payload = {}

response = payload.get("tool_response") or {}
exit_code = response.get("exitCode", response.get("exit_code", response.get("returncode")))
if exit_code not in (None, 0):
    sys.exit(0)

command = (payload.get("tool_input") or {}).get("command", "")
if "git commit" not in command:
    sys.exit(0)

message = (
    "A git commit just landed in this repo. Per the user's standing instruction, add or update "
    "today's row in WebAppME/docs/WebAppME_TaskSheet.xlsx (the 'Task Sheet' tab) to reflect this "
    "commit's work. Conventions to follow (see the existing rows, e.g. T-51 through T-66): S.No "
    "and Task No. (T-N) continue the existing sequence; Project/Plan, Category, and Priority match "
    "the nature of the work; Date is today; Status is 'Completed' if the commit finished the work, "
    "'In Progress' if more remains on the current branch. Copy per-cell formatting (font, borders, "
    "alignment, row height) from the row above with openpyxl's copy(cell._style) rather than "
    "reconstructing it; Status text-color is automatic via existing conditional formatting on "
    "column I, don't set fill manually. Ground the task description in `git log -1` and the actual "
    "diff, not just the raw commit message. Save immediately without asking for confirmation. If "
    "the file is locked (open in Excel), tell the user to close it and retry, don't skip silently. "
    "If today's row already covers this same work, update it instead of duplicating."
)

print(json.dumps({
    "hookSpecificOutput": {
        "hookEventName": "PostToolUse",
        "additionalContext": message,
    }
}))
