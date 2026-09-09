# Tests native Windows IDispatch on the real add-in source with synthetic API
# stubs. No vendor DLLs, no SOLIDWORKS installation and no COM registration.
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -ne 5 -or ![Environment]::Is64BitProcess) {
    throw 'Run using Windows PowerShell 5.1 x64.'
}
$root = Split-Path -Parent $PSScriptRoot
$work = Join-Path $env:TEMP ('SWSB callbacks ' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work -Force | Out-Null
$exe = Join-Path $work 'CallbackInteropSmoke.exe'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$process = $null
try {
    $compilerArguments = @(
        '/nologo', '/target:exe', '/platform:x64', '/codepage:65001',
        '/debug:pdbonly', "/out:$exe", '/reference:System.dll',
        '/reference:System.Core.dll', '/reference:System.Drawing.dll',
        '/reference:System.Windows.Forms.dll',
        (Join-Path $root 'src\SwAddin.cs'),
        (Join-Path $root 'src\SettingsForm.cs'),
        (Join-Path $PSScriptRoot 'CallbackInteropSmoke.cs')
    )
    & $csc @compilerArguments
    if ($LASTEXITCODE -ne 0) { throw 'Callback regression test compilation failed.' }

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
