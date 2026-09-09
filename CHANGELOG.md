# Changelog

## 1.0.7 — 2026-09-09

### Fixed

- Fix the v1.0.6 installation regression (`CS1502` / `CS1503`) in toolbar migration. Keep the `CommandTab` returned by `GetCommandTab` in a `CommandTab` variable; `RemoveCommandTab` requires that type, not `ICommandTab`.
- Keep the embedded original application icons and existing export behavior.

### Regression coverage

- Correct the test doubles for `GetCommandTab`, `AddCommandTab` and `RemoveCommandTab` to use `CommandTab`, with a distinct `ICommandTab` interface. The old doubles accepted a broader parameter and hid the real compilation failure.
- Add a negative-control compilation using a temporary copy of the production source with the old `ICommandTab` declaration restored. It must fail with `CS1503` before the corrected source is compiled and the existing icon, cache migration and COM callback tests run.
- These are limited API contract and Windows COM tests, not a build against vendor API DLLs or an end-to-end SOLIDWORKS session. Installation still compiles against the user's installed SOLIDWORKS API.

## 1.0.6 — 2026-09-09

### Original application icons inside the add-in

- Replace placeholder button images with the original upstream OrcaSlicer, Bambu Studio and PrusaSlicer artwork. Settings uses a neutral gear.
- Bundle the SVG sources and render them offline with Windows/.NET into 20, 32, 40, 64, 96 and 128 pixel PNG strips. No internet, Python, or third-party image package is needed during installation.
- Embed all twelve PNGs in the add-in DLL. Extract only those resources into a per-user, build-specific cache because the SOLIDWORKS API accepts image paths, not resource streams.
- Refresh only this add-in's two **3D Print** document tabs once, removing cached placeholder images. Subsequent launches preserve toolbar customization. Export logic and slicer paths are unchanged.
- Validate generated PNG dimensions, transparency, app colors/order, embedded-resource loading without external image files, cache repair, one-time migration, native COM callbacks and installation dependency deployment in Windows CI. A real SOLIDWORKS UI session remains a manual validation step.

## 1.0.5 — 2026-09-09

### Fixed

- Expose all six toolbar callbacks through an explicit COM-visible `ISlicerCallbacks` IDispatch interface with stable DISPIDs. Make it the add-in's default COM interface while preserving the existing `ISwAddin` lifecycle interface and class GUID.
- The old `ClassInterfaceType.None` class did not provide a dispatch contract for the toolbar callbacks. Registration alone could not validate this requirement.
- Compile the corrected source directly; remove install-time text rewriting. Keep the `SaveAs3` ref arguments and the `System.Environment` alias in source control.

### Diagnostics and validation

- Record each startup operation, loaded interop identity/location and full exception details to `%LOCALAPPDATA%\SolidWorksSlicerBridge\logs\addin.log`.
- Include the version, failed stage and HRESULT in the error dialog. Generate PDB symbols locally for useful stack traces.
- Add a Windows COM regression test with a negative control for the old callback shape. Compile the production source against limited API stubs, check native `IDispatch::GetIDsOfNames` for all six methods, invoke both enable callbacks through native `IDispatch::Invoke`, and exercise startup/teardown against the stub host.
- Neither the stubs nor their test executable are packaged. These tests do **not** constitute an end-to-end test inside SOLIDWORKS 2026; model export still needs host validation.

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
