# Changelog

## 1.0.4 — 2026-09-09

### Fixed

- Copy `SolidWorks.Interop.sldworks.dll`, `SolidWorks.Interop.swconst.dll` and `SolidWorks.Interop.swpublished.dll` from the local SOLIDWORKS API directory next to the compiled add-in before registration. Compiler `/reference` arguments alone do not deploy runtime dependencies.
- Verify every copied dependency with SHA-256 and record its assembly identity and destination in `install.log`.
- Preserve the previous PowerShell 5.1 warning-handling fix; a nonzero RegAsm exit code remains a fatal installation error.

### Validation and release process

- Add a disposable Windows PowerShell 5.1 regression test that reproduces a missing dependency and then executes the production build/install scripts with synthetic assemblies and real RegAsm.
- The test checks private dependency hashes and actual COM registration using a random test GUID, then unregisters the fixture. Synthetic assemblies are never included in the installer.
- This smoke test does **not** validate the full SOLIDWORKS host, its real API, the toolbar or model export. Those still require a machine with SOLIDWORKS 2026.
- Publish an installer SHA-256 file alongside each release executable.
- A change to `VERSION` on `main` triggers a release; tag and manual triggers remain available. Existing releases are not silently overwritten.

## 1.0.0 — 2026-09-08

- Initial public release for SOLIDWORKS 2026 x64.
- One-click 3MF export to OrcaSlicer, Bambu Studio and PrusaSlicer.
- Automatic slicer path detection with per-user overrides.
- CommandManager **3D Print** tab for parts and assemblies.
- Local build/install flow without Visual Studio.
- Release-grade x64 installer based on Inno Setup.
- GitHub Actions workflow for automatic Setup build and GitHub Release publishing.
