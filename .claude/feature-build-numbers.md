# Feature: build-numbers

## Goal
Replace the hardcoded version in `azure-pipelines.yml` with the `buildnumber.yml` template so that build numbers are automatically generated, matching the pattern used by Tharga.MongoDB.

## Scope
- Integrate `buildnumber.yml` as a template step in the pipeline
- Set `majorMinor` to `0.1`
- Remove the hardcoded `name: 0.0.12` version
- Ensure version still flows to AssemblyVersion, FileVersion, Version, and Chocolatey pack

## Acceptance Criteria
- [ ] `azure-pipelines.yml` uses `buildnumber.yml` template with `majorMinor: '0.1'`
- [ ] No hardcoded version number in the pipeline
- [ ] Build number flows correctly to all version properties and Chocolatey packaging
- [ ] Pipeline YAML is valid

## Done Condition
Pipeline uses auto-incrementing build numbers via `buildnumber.yml` template.
