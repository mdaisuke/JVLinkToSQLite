Top-Level
- Solution: JVLinkToSQLite.sln
- Build script: Build.ps1
- Wiki submodule: JVLinkToSQLite.wiki (docs for usage/specs)

Projects
- JVLinkToSQLite (Exe): CLI entrypoint and Windows Forms Setup UI.
  - Files: Program.cs, MainOptions(.cs/.Handler.cs), SettingOptions(.cs/.Handler.cs), SetupForm.*, logging and console helpers.
- Urasandesu.JVLinkToSQLite (Library): Core domain logic and JV-Link wrappers.
  - Key folders: 
    - JVLinkWrappers: Interop types + DataBridges (T4-based codegen for data mapping), result classes, exceptions.
    - Operators: Units of work to read JV data and write to SQLite.
    - OperatorAggregates: Orchestrate end-to-end operations over operators.
    - Settings: Strongly-typed config incl. data specs and runtime options.
    - Services: XmlSerializationService and interfaces.
- Urasandesu.JVLinkToSQLite.Basis (Library): Shared mixins/utilities for system, data, cryptography, and Cecil helpers.
- ObfuscatedResources (Library): T4-generated resource class + Obfuscar packaging meta.
- Tests:
  - Test.Urasandesu.JVLinkToSQLite: Unit/integration tests for operators, wrappers, etc.
  - Test.Urasandesu.JVLinkToSQLite.Basis: Tests for mixins and utilities.
  - Test.Urasandesu.JVLinkToSQLite.JVData: Codegen helpers/tests for JV data references.

Generated Code
- .tt/.t4 templates generate .g.cs files in Urasandesu.JVLinkToSQLite and ObfuscatedResources.
- TransformOnBuild is enabled; edit templates (not .g.cs) to change generated outputs.
