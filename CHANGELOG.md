# Changelog

## 1.0.9 — 2026-09-09

### Fixed

- Fix both `CS0023` compilation errors in `AppendExport.cs` introduced in v1.0.8. `ISldWorks.SetUserPreferenceToggle` returns `void`, not `bool`; call it as a statement and verify the resulting value with `GetUserPreferenceToggle`.
- Read back restored toggle values as well. A rejected setting, a restoration mismatch or an exception prevents dispatch, while restoration of the other saved settings is still attempted.
- Keep `SetUserPreferenceIntegerValue`'s Boolean result handling: it has a different API contract. Preserve all seven commands, original icons, normal 3MF export and existing-window delivery.

### Regression coverage

- Correct the shared API stubs to use a void toggle setter. Previously the incorrect Boolean stub allowed the invalid production expressions to compile in CI.
- Compile a temporary copy of the production source with both old expressions restored. It must produce two `CS0023` errors before the fixed source is built.
- Test silently ignored changes, an already-correct value, exceptions after partial mutation, restoration mismatches and restoration exceptions, in addition to the existing STL, COM, icon and cross-process IPC tests.
- These tests use limited API stubs and Windows receiver fixtures. They do not replace compilation against the installed SOLIDWORKS API or an end-to-end session in SOLIDWORKS and the actual slicers.

## 1.0.8 — 2026-09-09

### Added

- Three **Add to open ...** buttons for OrcaSlicer, Bambu Studio and PrusaSlicer, in addition to the existing 3MF launch buttons. Original application icons and existing command IDs are preserved.
- Discover initialized slicer windows in the current Windows session. Use the only matching window automatically, or let the user choose among multiple window titles/PIDs. Never silently start a new instance for an append request.
- Send geometry-only binary STL in millimeters through targeted `WM_COPYDATA`, using each slicer's existing import handler. Preserve the normal 3MF export path. Restore temporary STL preferences even when export fails.
- Support the legacy argument-list protocol and the PrusaSlicer JSON protocol introduced in 2.9.1, with explicit EXE-version detection and correctly escaped Unicode, spaces and semicolons.
- Reject stale recipients and disabled/modal main windows; bound delivery to five seconds and never automatically retry an uncertain delivery.
- Clean both `.3mf` and `.stl` exports older than seven days after successful export/dispatch. Temporary model directory: `%TEMP%\SolidWorksSlicerBridge`.
- [Usage, storage and compatibility details](docs/APPEND_TO_OPEN_WINDOW.md).

### Validation

- Cover nine native COM callback names, seven commands and one-time toolbar migration.
- Add actual cross-process Windows receiver fixtures for discovery, selected-window delivery, UTF-16 payloads, protocol escaping, zero WndProc results, PID mismatch, disabled windows, unrelated processes and timeouts.
- Simulate CAD export success/failures to check STL units, binary format and preference restoration. These fixtures are not real slicers or vendor API DLLs; an end-to-end CAD-to-slicer UI session is still a manual validation step.
- Include the exact source snapshot in the workflow artifact for reproducible inspection; no test executable is shipped in Setup.

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
