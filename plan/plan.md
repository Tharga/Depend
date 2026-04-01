# Plan

Two features are planned. They are independent and will be implemented on separate branches.

---

## Feature 1: build-numbers
**Branch:** `feature/build-numbers`
**Feature file:** `.claude/feature-build-numbers.md`

### Steps
- [x] 1. Update `azure-pipelines.yml` to use `buildnumber.yml` as a template step with `majorMinor: '0.1'`
- [x] 2. Remove the hardcoded `name: 0.0.12` line
- [x] 3. Verify the version variables (`Build.BuildNumber`) still flow to build, publish, and Chocolatey pack steps
- [x] 4. Commit: `feat: use buildnumber.yml template for auto-incrementing versions` (52f174f)

---

## Feature 2: package-analysis
**Branch:** `feature/package-analysis`
**Feature file:** `.claude/feature-package-analysis.md`

### Steps
- [ ] 1. Add `packages` (and shorthand `p`) to the output type options in `CommandService`
- [ ] 2. Add `--check-nuget` CLI flag parsing
- [ ] 3. Create `OutputPackagesService` with interface `IOutputPackagesService`
- [ ] 4. Implement `project.assets.json` reading to build full transitive dependency graph per project (reuse/refactor logic from `OutputTreeService.LoadResolvedGraphFromAssets`)
- [ ] 5. Implement redundancy detection: compare each project's direct PackageReferences against the transitive graph
- [ ] 6. Implement version comparison for color coding (red: transitive >= direct, yellow: transitive < direct)
- [ ] 7. Implement optional nuget.org latest version check (`--check-nuget`)
- [ ] 8. Wire up DI registration and `CommandService` switch case
- [ ] 9. Write tests for redundancy detection and version comparison logic
- [ ] 10. Update HELP file and README
- [ ] 11. Final commit: `feat: package-analysis complete`

---

## Status
- Feature 1 (build-numbers) is complete on branch `feature/build-numbers`, ready for review/merge
- Feature 2 (package-analysis) has not been started yet
