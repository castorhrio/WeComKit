# Contributing

This project aims to stay small and predictable.

## Development Rules

- Keep protocol helpers, HTTP APIs, and native SDK wrappers in separate packages.
- Do not add application-specific business rules to shared packages.
- Do not add runtime downloads for native dependencies.
- Add tests for behavior that can be verified without real WeCom credentials.
- Keep public API changes intentional while the project is in the `0.x` line.

## Local Checks

```powershell
dotnet test .\WeComKit.sln -c Release
dotnet pack .\WeComKit.sln -c Release --no-build -o .\artifacts\packages
```
