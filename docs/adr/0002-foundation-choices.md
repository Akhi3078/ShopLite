# 2. Foundation choices

**Date:** 2026-11-04
**Status:** Accepted

## Context

Four decisions had to be made before the first project existed, because each is expensive to reverse once ~13 projects reference it.

## Decisions

### Target framework: `net10.0`

.NET 10 is the current LTS, supported to 2028-11-14. .NET 9 reaches end of support on 2026-11-10 and .NET 8 on the same date. .NET 11 is at release candidate and is not a basis for learning.

The `net10.0` target is pinned in `Directory.Build.props` rather than per project.

### Central Package Management

`Directory.Packages.props` declares every NuGet version once; project files reference packages without versions. With this many projects, per-project versions drift between services and produce confusing runtime differences. Transitive pinning is enabled.

### Solution format: `.slnx`

The .NET CLI now generates the XML solution format by default. It is substantially less prone to merge conflicts than the legacy `.sln` format. Accepted as the default rather than forced back.

### Warnings are not errors

`TreatWarningsAsErrors` is off. In a learning repository it obstructs exploratory work. To be revisited once the service shape has settled.

## Consequences

- One place to change the framework version or any package version.
- `.slnx` requires reasonably current tooling; older Visual Studio versions cannot open it.
- Warnings will accumulate and need periodic attention, since nothing forces them to zero.
