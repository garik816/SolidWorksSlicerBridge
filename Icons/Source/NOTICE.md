# Original application artwork

These SVGs are original upstream application icons, not AI-generated replacements.
They are reproduced here solely to identify the supported slicer applications.
The bridge's MIT license does not relicense this third-party artwork. Application
names and logos remain the property of their respective owners; no endorsement
by those projects is implied. The original vector sources are included with the
installer and are the inputs to the offline renderer.

| File | Upstream project and original path | Upstream Git blob |
|---|---|---|
| OrcaSlicer.svg | https://github.com/OrcaSlicer/OrcaSlicer/blob/main/resources/images/OrcaSlicer.svg | f8d677c55578547745ad87ccb99d936ab6b1b04b |
| BambuStudio.svg | https://github.com/bambulab/BambuStudio/blob/master/resources/images/BambuStudio.svg | d269a08743154a7dd67238ff51a5e44404a48a2f |
| PrusaSlicer.svg | https://github.com/prusa3d/PrusaSlicer/blob/master/resources/icons/PrusaSlicer.svg | 927c3e70ba99b04792f432068c1474f1ca8d977f |

Upstream projects distribute their repositories under GNU AGPL version 3:
https://github.com/OrcaSlicer/OrcaSlicer/blob/main/LICENSE.txt
https://github.com/bambulab/BambuStudio/blob/master/LICENSE
https://github.com/prusa3d/PrusaSlicer/blob/master/LICENSE

Settings.svg is an original settings gear supplied under the bridge's MIT license.
Generate-Icons.ps1 rasterizes these simple SVGs with Windows/.NET WPF, preserving
the original shapes and colors. No web access, Python, or third-party renderer
is required on the user's computer. The twelve resulting PNGs are embedded in
SolidWorksSlicerBridge.dll. SOLIDWORKS requires filesystem paths, so the add-in
extracts those embedded resources into its own per-user, build-specific cache.
