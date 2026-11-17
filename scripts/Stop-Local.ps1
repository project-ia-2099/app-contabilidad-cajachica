$ErrorActionPreference = 'Stop'
$recordPath = Join-Path (Split-Path $PSScriptRoot -Parent) 'work/local-processes.json'
if (-not (Test-Path -LiteralPath $recordPath)) { throw 'No existe un registro local de procesos de esta aplicación.' }
foreach ($record in (Get-Content -LiteralPath $recordPath -Raw | ConvertFrom-Json)) {
    $process = Get-Process -Id $record.id -ErrorAction SilentlyContinue
    if ($process -and $process.ProcessName -eq $record.name -and $process.StartTime.ToUniversalTime().ToString('O') -eq $record.started) {
        $process.Kill()
        Write-Output "Detenido el componente $($record.name) de caja chica."
    }
}
