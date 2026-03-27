# Copilot Work Instructions

## Temporary Workspace Protocol

⚠️ **CRITICAL**: This workspace is temporary. Changes must be committed and pushed frequently to avoid data loss.

### Commit and Push Rules

1. **After each working feature/fix**: Commit and push changes immediately upon completion
   - Example: After implementing a feature → commit → push
   - Example: After fixing a bug → commit → push
   - Example: After successful test run → commit → push

2. **Use descriptive commit messages**
   - Format: `<type>: <description>` (e.g., `feat: add game state persistence`, `fix: resolve card deck shuffle`)
   - Include what was changed and why when relevant

3. **Never leave uncommitted work**
   - Always commit before ending a work session
   - Always push before closing the workspace
   - Check status: `git status` before and after major changes

### Git Workflow

```bash
# Regular workflow
git add .
git commit -m "feat: describe what you did"
git push origin main

# Before starting new work
git pull origin main

# Verify status
git status
git log --oneline -5
```

### When to Commit/Push

- ✅ After implementing a working feature
- ✅ After fixing a bug
- ✅ After completing a test suite
- ✅ After updating documentation
- ✅ End of work session (even if incomplete, with "wip:" prefix if needed)
- ❌ Never leave for extended periods without pushing

### Branch Strategy

- Work on `main` branch only (single main branch workflow)
- No feature branches needed for temporary workspace
- All completed work goes directly to `main`

---

**Last Updated**: March 27, 2026
