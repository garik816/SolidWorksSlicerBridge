# Tests native Windows IDispatch and embedded icons on the real add-in source
# with synthetic API stubs. No vendor DLLs or SOLIDWORKS installation are used.
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -ne 5 -or ![Environment]::Is64BitProcess) {
    throw 'Run using Windows PowerShell 5.1 x64.'
}
$root = Split-Path -Parent $PSScriptRoot
$work = Join-Path $env:TEMP ('SWSB callbacks ' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work -Force | Out-Null
$exe = Join-Path $work 'CallbackInteropSmoke.exe'
$iconDirectory = Join-Path $work 'Icons'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$process = $null
try {
    & (Join-Path $root 'Generate-Icons.ps1') -OutputDirectory $iconDirectory
    $resources = @()
    foreach ($file in (Get-ChildItem -LiteralPath $iconDirectory -Filter '*.png' -File)) {
        $resources += "/resource:$($file.FullName),SolidWorksSlicerBridge.Icons.$($file.Name)"
    }
    if ($resources.Count -ne 12) { throw 'Expected twelve generated toolbar resources.' }
    $sources = @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' -File |
        Sort-Object Name | Select-Object -ExpandProperty FullName)
    $compilerArguments = @(
        '/nologo', '/target:exe', '/platform:x64', '/codepage:65001',
        '/debug:pdbonly', "/out:$exe", '/reference:System.dll',
        '/reference:System.Core.dll', '/reference:System.Drawing.dll',
        '/reference:System.Windows.Forms.dll',
        (Join-Path $PSScriptRoot 'CallbackInteropSmoke.cs')
    ) + $sources + $resources

    # Reintroduce only the v1.0.6 type mismatch in a temporary source copy.
    # The corrected test doubles must reject it with the user's CS1503 error;
    # this prevents an overly permissive API stub from masking the regression.
    $addinSource = Join-Path $root 'src\SwAddin.cs'
    $sourceText = Get-Content -LiteralPath $addinSource -Raw -Encoding UTF8
    $correct = 'CommandTab tab = commandManager.GetCommandTab'
    if ([regex]::Matches($sourceText, [regex]::Escape($correct)).Count -ne 1) {
        throw 'Expected exactly one typed CommandTab acquisition for the regression control.'
    }
    $negativeSource = Join-Path $work 'SwAddin.negative.cs'
    $sourceText.Replace($correct, 'ICommandTab tab = commandManager.GetCommandTab') |
        Set-Content -LiteralPath $negativeSource -Encoding UTF8
    $negativeExe = Join-Path $work 'NegativeCommandTab.exe'
    $negativeArguments = foreach ($argument in $compilerArguments) {
        if ($argument -eq $addinSource) { $negativeSource }
        elseif ($argument -eq "/out:$exe") { "/out:$negativeExe" }
        else { $argument }
    }
    $negativeLog = Join-Path $work 'negative-compilation.log'
    $oldPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        & $csc @negativeArguments > $negativeLog 2>&1
        $negativeExit = $LASTEXITCODE
    }
    finally { $ErrorActionPreference = $oldPreference }
    $negativeText = Get-Content -LiteralPath $negativeLog -Raw
    if ($negativeExit -eq 0 -or $negativeText -notmatch 'error CS1503:.*ICommandTab.*CommandTab') {
        throw "The negative control did not reproduce the CommandTab type error: $negativeText"
    }
    $unexpected = [regex]::Matches($negativeText, 'error (CS\d+):') |
        Where-Object { $_.Groups[1].Value -notin @('CS1502', 'CS1503') }
    if ($unexpected) { throw "Unrelated errors in negative control: $negativeText" }
    Write-Host 'PASS negative control: v1.0.6 ICommandTab argument is rejected with CS1503.'

    & $csc @compilerArguments
    if ($LASTEXITCODE -ne 0) { throw 'Callback regression test compilation failed.' }
    Write-Host 'PASS: corrected production source compiles against the strict CommandTab contract.'

    # Prove the running add-in reads images from its assembly, not build folders.
    Remove-Item -LiteralPath $iconDirectory -Recurse -Force
    $start = New-Object System.Diagnostics.ProcessStartInfo
    $start.FileName = $exe
    $start.WorkingDirectory = $work
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $start
    if (!$process.Start()) { throw 'Could not start callback tests.' }
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    if (!$process.WaitForExit(60000)) {
        $process.Kill()
        $process.WaitForExit()
        throw 'Callback tests timed out after 60 seconds.'
    }
    $process.WaitForExit()
    Write-Host $stdout.Result
    Write-Host $stderr.Result
    if ($process.ExitCode -ne 0) { throw "Callback tests failed: $($process.ExitCode)" }
}
finally {
    if ($process) { $process.Dispose() }
    if (Test-Path -LiteralPath $work) { Remove-Item -LiteralPath $work -Recurse -Force }
}
