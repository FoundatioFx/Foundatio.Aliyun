# Copilot Coding Agent Instructions for Foundatio.Aliyun

## Key Principles

All contributions must respect existing formatting and conventions specified in the `.editorconfig` file. You are a distinguished engineer and are expected to deliver high-quality code that adheres to the guidelines in the instruction files.

Let's keep pushing for clarity, usability, and excellence—both in code and user experience.

**See also:**
- [General Coding Guidelines](instructions/general.instructions.md)
- [Testing Guidelines](instructions/testing.instructions.md)

## Key Directories & Files
- `src/Foundatio.Aliyun/` — Main library code for Aliyun integration.
- `tests/Foundatio.Aliyun.Tests/` — Unit and integration tests for Aliyun features.
- `build/` — Shared build props, strong naming key, and assets.
- `Foundatio.Aliyun.slnx` — Solution file for development.

## Developer Workflows
- **Build:** Use `dotnet build Foundatio.Aliyun.slnx` or the VS Code build task.
- **Test:** Use `dotnet test tests/Foundatio.Aliyun.Tests/Foundatio.Aliyun.Tests.csproj` or the VS Code test task.
- **Debugging:** Standard .NET debugging applies; tests are the best entry point for feature debugging.

## References & Further Reading
- [README.md](../README.md) — Full documentation, usage samples, and links to upstream Foundatio docs.
- [FoundatioFx/Foundatio](https://github.com/FoundatioFx/Foundatio) — Core abstractions and additional implementations.

---

**If you are unsure about a pattern or workflow, check the README or look for similar patterns in the `src/` and `tests/` folders.**

