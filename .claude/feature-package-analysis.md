# Feature: package-analysis

## Goal
Add a new `--output packages` mode that analyzes NuGet package health across projects, detecting redundant transitive dependencies and optionally checking nuget.org for outdated packages.

## Scope
- New output mode: `--output packages` (shorthand `p`)
- Detect redundant direct references: if project X references package A which transitively brings in package B, and project X also directly references B, flag B as redundant
  - Red: transitive version >= directly referenced version (safe to remove)
  - Yellow: transitive version < directly referenced version (review needed)
- Optional `--check-nuget` flag to query nuget.org for latest versions
- Use `project.assets.json` for resolved transitive dependency data
- Show a focused, actionable per-project report

## Acceptance Criteria
- [ ] `--output packages` mode exists and is documented in help
- [ ] Redundant transitive dependencies are detected and displayed
- [ ] Red color for redundant deps where transitive version >= direct version
- [ ] Yellow color for redundant deps where transitive version < direct version
- [ ] `--check-nuget` flag queries nuget.org for latest package versions
- [ ] Works across all scanned repositories
- [ ] Tests cover redundancy detection logic and version comparison
- [ ] README updated

## Done Condition
Running `depend <path> --output packages` shows per-project analysis of redundant and optionally outdated NuGet packages.
