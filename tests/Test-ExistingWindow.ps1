# Real Windows IPC receivers with synthetic slicer identities and CAD API stubs.
# No installed SOLIDWORKS or third-party slicer is exercised by these tests.
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or $PSVersionTable.PSVersion.Major -ne 5 -or ![Environment]::Is64BitProcess) {
    throw 'Run on a disposable x64 GitHub Actions runner using Windows PowerShell 5.1.'
}
$root = Split-Path -Parent $PSScriptRoot
$work = Join-Path $env:TEMP ('SWSB append validation ' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work -Force | Out-Null
$exe = Join-Path $work 'ExistingWindowSmoke.exe'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$process = $null
try {
    $sources = @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' -File |
        Sort-Object Name | Select-Object -ExpandProperty FullName)
    $commonArguments = @(
        '/nologo', '/target:exe', '/platform:x64', '/codepage:65001',
        '/debug:pdbonly', '/main:ExistingWindowSmoke',
        '/reference:System.dll', '/reference:System.Core.dll',
        '/reference:System.Drawing.dll', '/reference:System.Windows.Forms.dll',
        '/reference:System.Web.Extensions.dll',
        (Join-Path $PSScriptRoot 'CallbackInteropSmoke.cs'),
        (Join-Path $PSScriptRoot 'ExistingWindowSmoke.cs')
    )

    # Restore the two invalid expressions from v1.0.8 in a temporary copy only.
    # Both must fail against the void setter contract before testing the fix.
    $appendSource = Join-Path $root 'src\AppendExport.cs'
    $brokenSource = Join-Path $work 'AppendExport.regression.cs'
    $text = Get-Content -LiteralPath $appendSource -Raw -Encoding UTF8
    foreach ($value in @('requested', 'saved')) {
        $call = "app.SetUserPreferenceToggle(toggles[i], $value[i]);"
        $check = "app.GetUserPreferenceToggle(toggles[i]) != $value[i]"
        if (!$text.Contains($call) -or !$text.Contains($check)) {
            throw "Could not construct $value toggle negative control from production source."
        }
        $text = $text.Replace($call, '').Replace($check, "!app.SetUserPreferenceToggle(toggles[i], $value[i])")
    }
    Set-Content -LiteralPath $brokenSource -Value $text -Encoding UTF8
    $negativeSources = @($sources | Where-Object { $_ -ne $appendSource }) + @($brokenSource)
    $negativeArguments = $commonArguments + @("/out:$work\MustNotCompile.exe") + $negativeSources
    $oldErrorPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $negativeOutput = @(& $csc @negativeArguments 2>&1)
        $negativeCode = $LASTEXITCODE
    }
    finally { $ErrorActionPreference = $oldErrorPreference }
    $negativeOutput | ForEach-Object { Write-Host $_ }
    $toggleErrors = @($negativeOutput | Where-Object { $_ -match 'AppendExport\.regression\.cs.*error CS0023:' })
    if ($negativeCode -eq 0 -or $toggleErrors.Count -ne 2) {
        throw 'Negative control did not reproduce both void-setter CS0023 failures.'
    }
    Write-Host 'PASS: both v1.0.8 void-toggle expressions are rejected with CS0023.'

    $compilerArguments = $commonArguments + @("/out:$exe") + $sources
    & $csc @compilerArguments
    if ($LASTEXITCODE -ne 0) { throw 'Append regression tests did not compile.' }
    $start = New-Object System.Diagnostics.ProcessStartInfo
    $start.FileName = $exe
    $start.WorkingDirectory = $work
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $start
    if (!$process.Start()) { throw 'Could not start append tests.' }
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    if (!$process.WaitForExit(120000)) {
        & taskkill.exe /PID $process.Id /T /F | Out-Null
        throw 'Append tests exceeded their 120 second timeout.'
    }
    $process.WaitForExit()
    Write-Host $stdout.Result
    Write-Host $stderr.Result
    if ($process.ExitCode -ne 0) { throw "Append tests failed: $($process.ExitCode)" }
}
finally {
    if ($process) { $process.Dispose() }
    if (Test-Path -LiteralPath $work) { Remove-Item -LiteralPath $work -Recurse -Force }
}
