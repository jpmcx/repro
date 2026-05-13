# Environment Summary

Validated on 2026-05-13.

## Platform

- OS product name: `Windows 10 Pro`
- Display version: `24H2`
- OS build: `26100.8246`
- OS version reported by `dotnet --info`: `10.0.26100`
- Architecture: `x64` / `win-x64`

## .NET Toolchain

- Active SDK selected inside the repro folder: `10.0.203`
- SDK commit: `c23858a6d8`
- MSBuild version: `18.3.3+c23858a6d`
- Workload manifest version: `10.0.200-manifests.f8ca1cb9`
- Host runtime version: `10.0.7`
- Host runtime architecture: `x64`

## Repro Package

- Project target framework: `net10.0`
- Pinned package: `Microsoft.CodeAnalysis.CSharp.Scripting 5.3.0`
- SDK pinning file: `global.json` with `10.0.203` and `rollForward: disable`

## Validation Notes

- `dotnet --version` inside `tmp/roslyn-runtime-repro/` resolved to `10.0.203`
- `dotnet --info` inside `tmp/roslyn-runtime-repro/` reported the active `global.json` path as `D:\dev\sbx\tmp\roslyn-runtime-repro\global.json`
