Windows: Build and Package
- Developer Command Prompt (VS 2022, Admin): open before running commands.
- Restore NuGet: `nuget restore JVLinkToSQLite.sln`
- Build Release: `msbuild JVLinkToSQLite.sln /p:Configuration=Release /m`
- Package (SFX exe): `powershell -ExecutionPolicy Bypass -File .\Build.ps1 -Package -BuildTarget Rebuild`
- Update wiki submodule: `git submodule update --init --recursive --remote "JVLinkToSQLite.wiki"`

Windows: Run App
- Show about: `.\JVLinkToSQLite\bin\Release\JVLinkToSQLite.exe main --mode About`
- Initialize (GUI): `.\JVLinkToSQLite\bin\Release\JVLinkToSQLite.exe main --mode Init`
- Generate default setting: `.\JVLinkToSQLite\bin\Release\JVLinkToSQLite.exe main --mode DefaultSetting -s setting.xml`
- Execute conversion: `.\JVLinkToSQLite\bin\Release\JVLinkToSQLite.exe main --mode Exec -s setting.xml -d .\race.db -t 100`
- Watch event mode: `.\JVLinkToSQLite\bin\Release\JVLinkToSQLite.exe main --mode Event -s setting.xml`
- Update setting via XPath: `.\JVLinkToSQLite\bin\Release\JVLinkToSQLite.exe setting -s setting.xml -x "//XPath" -v "<NewValue>" -f`

Windows: Run Tests
- From Visual Studio: Test Explorer (NUnit 3 adapter included).
- From CLI (example):
  - `"%ProgramFiles(x86)%\Microsoft Visual Studio\2022\Professional\Common7\IDE\Extensions\TestPlatform\vstest.console.exe" .\Test.Urasandesu.JVLinkToSQLite\bin\Release\Test.Urasandesu.JVLinkToSQLite.dll .\Test.Urasandesu.JVLinkToSQLite.Basis\bin\Release\Test.Urasandesu.JVLinkToSQLite.Basis.dll`

macOS (Darwin): Editing Only
- Building/running .NET Framework 4.8 is not supported; use Windows for build/test/run.
- Useful shell helpers: `ls`, `cd`, `find . -name "*.csproj"`, `grep -R "JVLinkToSQLite" -n .`, `sed -n '1,120p' file.cs`.
