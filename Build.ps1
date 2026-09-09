$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Out = Join-Path $Root "build"
New-Item -ItemType Directory -Force -Path $Out | Out-Null

$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (!(Test-Path $csc)) {
    throw ".NET Framework 4.x x64 C# compiler not found: $csc"
}

$searchRoots = @(
    "$env:ProgramFiles\SOLIDWORKS Corp\SOLIDWORKS\api\redist",
    "$env:ProgramFiles\Dassault Systemes\SOLIDWORKS 2026\api\redist",
    "$env:ProgramFiles\SOLIDWORKS Corp\SOLIDWORKS 2026\api\redist"
)

$apiDir = $null
foreach ($p in $searchRoots) {
    if (Test-Path (Join-Path $p "SolidWorks.Interop.sldworks.dll")) {
        $apiDir = $p
        break
    }
}

if (!$apiDir) {
    $found = Get-ChildItem "$env:ProgramFiles" -Filter "SolidWorks.Interop.sldworks.dll" -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match "api\\redist" } |
        Select-Object -First 1
    if ($found) { $apiDir = $found.DirectoryName }
}

if (!$apiDir) {
    throw "SOLIDWORKS API interop DLLs not found. SOLIDWORKS 2026 must be installed."
}

$refs = @(
    (Join-Path $apiDir "SolidWorks.Interop.sldworks.dll"),
    (Join-Path $apiDir "SolidWorks.Interop.swconst.dll"),
    (Join-Path $apiDir "SolidWorks.Interop.swpublished.dll")
)

foreach ($r in $refs) {
    if (!(Test-Path $r)) { throw "Missing reference: $r" }
}

# Compile the actual checked-in source: no source rewriting at installation time.
$dll = Join-Path $Out "SolidWorksSlicerBridge.dll"
$sources = @(
    (Join-Path $Root "src\SwAddin.cs"),
    (Join-Path $Root "src\SettingsForm.cs")
)

$compilerArgs = @(
    "/nologo",
    "/target:library",
    "/platform:x64",
    "/codepage:65001",
    "/debug:pdbonly",
    "/optimize+",
    "/out:$dll",
    "/reference:System.dll",
    "/reference:System.Core.dll",
    "/reference:System.Drawing.dll",
    "/reference:System.Windows.Forms.dll",
    "/reference:$($refs[0])",
    "/reference:$($refs[1])",
    "/reference:$($refs[2])"
) + $sources

& $csc $compilerArgs
if ($LASTEXITCODE -ne 0) { throw "C# build failed." }

# /reference only locates assemblies for the compiler. RegAsm is a separate
# process and must be able to load those exact dependencies beside our DLL.
# Copy from the local SOLIDWORKS installation; never download or register the
# vendor interops, modify the GAC, or change SOLIDWORKS's original files.
foreach ($reference in $refs) {
    $destination = Join-Path $Out ([IO.Path]::GetFileName($reference))
    $sourceHash = (Get-FileHash -LiteralPath $reference -Algorithm SHA256).Hash
    Copy-Item -LiteralPath $reference -Destination $destination -Force
    $copiedHash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
    if ($copiedHash -ne $sourceHash) {
        throw "Runtime dependency copy verification failed: $destination"
    }
    $identity = [Reflection.AssemblyName]::GetAssemblyName($destination).FullName
    Write-Host "Runtime dependency: $identity"
    Write-Host "Copied to: $destination; SHA256: $copiedHash"
}

$iconDest = Join-Path $Out "Icons"
New-Item -ItemType Directory -Force -Path $iconDest | Out-Null
Copy-Item (Join-Path $Root "Icons\*.png") $iconDest -Force

Write-Host "Built: $dll" -ForegroundColor Green
Write-Host "SOLIDWORKS API: $apiDir"
