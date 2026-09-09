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
    & $csc @compilerArguments
    if ($LASTEXITCODE -ne 0) { throw 'Callback regression test compilation failed.' }

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
