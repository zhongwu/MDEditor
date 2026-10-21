---
name: mdeditor-git
description: Git workflow rules for MDEditor project. Use when committing, pushing, or managing git operations. Enforces explicit user approval before any push.
allowed-tools: Bash, Read, Glob, Grep
---

# MDEditor Git Workflow

## Critical Rules

1. **NEVER commit or push without explicit user approval.**
   - The user must say "push", "looks good, push", or similar explicit approval
   - Even if the user says "looks good", wait for explicit "push" instruction
   - Never assume approval from context

2. **Always build and verify** changes before reporting to the user.

3. **Report results** and wait for user review before proceeding to commit/push.

4. **When the user reports an issue**, fix it, build, report results, and wait for approval before pushing.

## Workflow

1. Make changes locally
2. Build and verify (`dotnet build`)
3. Report results to user
4. **Wait for explicit "push" or "looks good, push" approval**
5. Only then commit and push

## Commit Message Format

```
feat: description (for new features)
fix: description (for bug fixes)
docs: description (for documentation)
refactor: description (for refactoring)
```

## Branch Strategy

- Main branch: `main`
- Feature branches: `feature/description`
- No direct pushes to main without review

## Repository

- GitHub: https://github.com/zhongwu/MDEditor
- Local path: C:\Users\zwu_9\Documents\projects\MKEditor