After You Make Changes
- Restore packages: run `nuget restore JVLinkToSQLite.sln` if you added/updated NuGet packages.
- Rebuild: `msbuild JVLinkToSQLite.sln /p:Configuration=Release /m` (Windows Developer Command Prompt for VS 2022).
- Regenerate code: if you edited any .tt/.t4 templates, ensure TransformOnBuild is on and rebuild; verify .g.cs updates.
- Run tests: use Visual Studio Test Explorer or `vstest.console.exe` against built test DLLs.
- Manual run: run the app with `JVLinkToSQLite.exe main --mode About` or `--mode Exec` to sanity check CLI.
- Packaging (optional): `powershell -ExecutionPolicy Bypass -File .\Build.ps1 -Package -BuildTarget Rebuild` to produce an artifact under `work/`.
- Docs (optional): update wiki submodule and regenerate PDF via Build.ps1 with `-WithDocument` (requires Node + wkhtmltopdf).
- Verify JV-Link interop: if features touch JV-Link, validate on a Windows machine with JV-Link installed.
