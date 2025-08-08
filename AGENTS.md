# Repository Guidelines

## Project Structure & Module Organization
- `JVLinkToSQLite.sln`: Solution entry.
- `JVLinkToSQLite/`: CLI/WinForms app and options handling.
- `Urasandesu.JVLinkToSQLite/`: Core services (DI via DryIoc, operators, wrappers).
- `Urasandesu.JVLinkToSQLite.Basis/`: Shared utilities and mixins.
- `Urasandesu.JVLinkToSQLite.JVData/`: JV-Link interop and data specifications.
- `Test.Urasandesu.*`: NUnit test projects for each module.
- `ObfuscatedResources/`: T4 and Obfuscar pipeline used at build time.
- `JVLinkToSQLite.wiki/`: Docs (git submodule).

## Build, Test, and Development Commands
- Prerequisites (Windows): VS 2022 Developer Command Prompt, NuGet CLI, 7‑Zip. Docs build also needs Node.js, wkhtmltopdf, and the wiki submodule.
- Restore + build (Release): `nuget restore JVLinkToSQLite.sln && msbuild JVLinkToSQLite.sln /m /p:Configuration=Release`
- Scripted build/package: `powershell -ExecutionPolicy Bypass -File .\Build.ps1 -BuildTarget Rebuild`
- With docs: `powershell -File .\Build.ps1 -WithDocument`
- Artifact: `work/JVLinkToSQLiteArtifact_*.exe` (self‑extracting package).

## Coding Style & Naming Conventions
- C# (.NET Framework 4.8), 4‑space indentation, Allman braces.
- Naming: PascalCase for types/methods, camelCase for locals/parameters, ALL_CAPS only for constants.
- Prefer explicit access modifiers; keep methods small and DI‑friendly (DryIoc).
- Follow existing directory and namespace layout; mirror it for new code.

## Testing Guidelines
- Framework: NUnit 3 with NUnit3TestAdapter; mocks via NSubstitute.
- Test naming: files end with `*Test.cs`; classes `*Test`; `[Test]` methods are imperative and focused.
- Run in VS Test Explorer or CLI, e.g.: `vstest.console.exe Test.Urasandesu.JVLinkToSQLite\bin\Release\Test.Urasandesu.JVLinkToSQLite.dll /Settings:Test.Urasandesu.JVLinkToSQLite\Test.Urasandesu.JVLinkToSQLite.runsettings`
- Add tests for new logic and edge cases; avoid coupling to JV‑Link unless necessary.

## Commit & Pull Request Guidelines
- Commits: short, imperative titles; English or Japanese are fine; use `[WIP]` while iterating; reference issues with `#123`.
- PRs: include what/why, how to test, linked issues, and screenshots/logs for UX or output changes. Note if you modify `Build.ps1` or packaging.

## Security & Configuration Tips
- Do not commit JV‑Link binaries, license keys, or generated DB files.
- Packaging keys: pass `-AuthorId`, `-SoftwareId`, `-PublicKey` to `Build.ps1` locally; never commit secrets.
- For docs, ensure the wiki is present: `git submodule update --init --remote JVLinkToSQLite.wiki`.

