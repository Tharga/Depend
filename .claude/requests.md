# Requests

## Pending

### Migrate from Azure Pipelines to GitHub Actions
- **From:** Notes/Obsidian (project management)
- **Date:** 2026-03-26
- **Priority:** Medium
- **Status:** Pending

**Goal:** Replace Azure Pipelines with GitHub Actions while preserving the current build number system.

**Current setup (Azure Pipelines):**
- `buildnumber.yml` — auto-increments patch version based on previous builds
  - Master branch: `{majorMinor}.{patch}` (e.g. `0.1.42`)
  - Feature branch: `{majorMinor}.{patch}-pre.{counter}` (e.g. `0.1.42-pre.1`)
- `azure-pipelines.yml` — two-stage pipeline:
  1. **Build stage:** restore → build → test → publish win-x64 → create Chocolatey package
  2. **Release stage** (master only): push to Chocolatey + create GitHub Release with tag `v{version}`
- Version is injected via `/p:Version=$(Build.BuildNumber)`

**Requirements for GitHub Actions:**
1. Same version numbering logic (auto-increment patch, pre-release for branches)
2. Build, test, publish win-x64 self-contained binary
3. Create Chocolatey package and push to chocolatey.org
4. Create GitHub Release with assets and changelog
5. **Manual approval** required before releasing to Chocolatey (use GitHub Environments with required reviewers)
6. Use **GitHub Packages** as a staging feed to test .nupkg before pushing to Chocolatey (replaces Azure DevOps Artifacts for pre-release testing)
7. Secrets needed: `CHOCOLATEY_API_KEY`

**Why:** GitHub Actions is closer to the code for open source projects. If this works well, it becomes the template for migrating all Tharga open source projects.

## Notifications
