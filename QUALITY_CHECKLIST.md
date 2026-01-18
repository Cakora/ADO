## CI / Quality Gates (Required)

- Build: `dotnet build AdoAsync.sln -c Release`
- Tests: `dotnet test AdoAsync.sln -c Release`
- Analyzers: warnings-as-errors already enabled (`AdoAsync.csproj`)
- SonarQube: run analysis in CI and treat findings as blocking (configure project key/token/host in pipeline)
- Packaging: `dotnet pack src/AdoAsync/AdoAsync.csproj -c Release` (fail on pack errors; add README to package)
- Vulnerability scan: enable NuGet vulnerability scanning in CI and fail on findings
- Branch protection: require the above checks on main/dev
- Traceability: update `docs/requirements-touched.md` per change set
