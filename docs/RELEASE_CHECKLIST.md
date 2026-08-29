# Release checklist

This checklist records release gates that must be completed for the artifact
under review. LUNA-21 owns the dependency and supply-chain section; packaging,
signing, bundled-data, recovery, and CI sections remain in later release
packets.

## Dependency and supply-chain gate

- [ ] Run `dotnet restore ProjectCostForecast.sln --locked-mode` from the
  repository root.
- [ ] Run
  `powershell.exe -NoProfile -ExecutionPolicy Bypass -File
  '.\scripts\audit-dependencies.ps1' -NoRestore -FailOnVulnerability
  -OutputPath 'docs/audit/LUNA-21-DEPENDENCY-AUDIT.json'` with access to the
  configured NuGet advisory source.
- [ ] Review the generated direct/transitive inventory and confirm that
  `VulnerabilityCount` is zero, or record owner, rationale, mitigation, and
  expiry for every approved exception.
- [ ] Review the package license metadata below and preserve the applicable
  notices in the release channel.

The current LUNA-21 inventory was captured on 2026-08-29 with .NET SDK
10.0.202. All packages use the package-declared license metadata from the
resolved `.nuspec` files in the local global-packages cache; the two packages
without an SPDX expression retain their declared license URL for manual
review.

| Package | Version | Use | License metadata |
|---|---:|---|---|
| ClosedXML | 0.105.1 | Production, direct | MIT |
| ClosedXML.Parser | 2.0.0 | Production, transitive | MIT |
| DocumentFormat.OpenXml | 3.1.1 | Production, transitive | MIT |
| DocumentFormat.OpenXml.Framework | 3.1.1 | Production, transitive | MIT |
| ExcelNumberFormat | 1.1.0 | Production, transitive | MIT |
| RBush.Signed | 4.0.0 | Production, transitive | MIT |
| SixLabors.Fonts | 1.0.0 | Production, transitive | Apache-2.0 |
| System.IO.Packaging | 8.0.1 | Production, transitive | MIT |
| Microsoft.NET.Test.Sdk | 17.11.1 | Test, direct | MIT |
| Microsoft.CodeCoverage | 17.11.1 | Test, transitive | MIT |
| Microsoft.TestPlatform.ObjectModel | 17.11.1 | Test, transitive | MIT |
| Microsoft.TestPlatform.TestHost | 17.11.1 | Test, transitive | MIT |
| Newtonsoft.Json | 13.0.1 | Test, transitive | MIT |
| System.Reflection.Metadata | 1.6.0 | Test, transitive | See declared license URL |
| xunit | 2.9.2 | Test, direct | Apache-2.0 |
| xunit.abstractions | 2.0.3 | Test, transitive | See declared license URL |
| xunit.analyzers | 1.16.0 | Test, transitive | Apache-2.0 |
| xunit.assert | 2.9.2 | Test, transitive | Apache-2.0 |
| xunit.core | 2.9.2 | Test, transitive | Apache-2.0 |
| xunit.extensibility.core | 2.9.2 | Test, transitive | Apache-2.0 |
| xunit.extensibility.execution | 2.9.2 | Test, transitive | Apache-2.0 |
| xunit.runner.visualstudio | 2.8.2 | Test, direct | Apache-2.0 |

The machine-readable package paths, resolved versions, direct/transitive
project usage, NuGet source response, repository commit, and vulnerability
result are retained in
[`docs/audit/LUNA-21-DEPENDENCY-AUDIT.json`](audit/LUNA-21-DEPENDENCY-AUDIT.json).
