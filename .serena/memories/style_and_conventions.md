Coding Style
- C# for .NET Framework 4.8; typical PascalCase for types/methods, camelCase for locals/parameters; minimal underscores.
- No .editorconfig found; follow standard Visual Studio C# conventions and project defaults.
- Comments/documentation are concise; many user-facing strings are Japanese.

Organization & Patterns
- Dependency Injection via DryIoc; create a root container and scope child containers per operation.
- Logging/Messaging through JVOperationMessenger static helpers; Info/Warning/Error/Verbose patterns.
- Options parsing via commandlineparser with verbs ("main" default, "setting").
- T4 Templates (.tt/.t4) generate .g.cs; do not edit .g.cs; edit templates and rebuild.
- Tests use NUnit 3 attributes/assertions; adapters included for VS integration.

Data/Interop Conventions
- SQLite interactions encapsulated in operators and mixins (prepared commands, parameter collections, etc.).
- JV-Link interop types wrapped under JVLinkWrappers; exceptions and result-value objects represent native interactions.

PR/Change Hygiene
- Keep changes scoped per project; update templates if affecting generated code.
- Japanese end-user messages should remain coherent and consistent in tone.
- If adding dependencies, use packages.config conventions and ensure NuGet restore succeeds.
