Project: JVLinkToSQLite

Purpose
- Convert JRA-VAN DataLab JV-Link horse racing data into a local SQLite database for analysis and use.

Tech Stack
- Language/Runtime: C# targeting .NET Framework 4.8 (Windows-only).
- App type: Console app with a small Windows Forms UI for initialization (Setup form).
- Data: System.Data.SQLite + Entity Framework 6 (code uses EF6 and direct SQLite commands).
- DI: DryIoc.
- CLI: commandlineparser.
- Code generation: T4 templates (.tt, .t4) generating .g.cs files.
- Build/Packaging: PowerShell script (Build.ps1), NuGet restore, MSBuild, 7-Zip SFX, optional wiki-to-PDF via wkhtmltopdf + Node (Windows-only requirements).
- Testing: NUnit 3 + NUnit3TestAdapter (built with MSBuild; run via VS Test Explorer or vstest.console).
- Utilities/Libraries: Mono.Cecil, Obfuscar (resource obfuscation project).

OS, Tooling, External Requirements
- Build/run requires Windows (Developer Command Prompt for VS 2022, admin). macOS/Linux cannot build .NET Framework 4.8.
- NuGet CLI, MSBuild, 7-Zip, and optionally Node.js + wkhtmltopdf are needed for full packaging.
- JV-Link (JRA-VAN DataLab) must be installed on the target machine to actually fetch/convert data.
- Some builds/tests expect Interop.JVDTLabLib.dll and Urasandesu.JVLinkToSQLite.JVData.dll under Urasandesu.JVLinkToSQLite.JVData\bin (these are not part of this repo).
