param([int]$ApiPort = 5100, [int]$WebPort = 5173)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if ([string]::IsNullOrWhiteSpace($env:ConnectionStrings__DefaultConnection)) {
    throw 'No está configurada ConnectionStrings__DefaultConnection. Solicite a TI la configuración local; no escriba credenciales en el repositorio.'
}
if ((node --version) -notmatch '^v26\.') { throw 'Esta aplicación requiere Node.js 26.' }
foreach ($port in @($ApiPort, $WebPort)) {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $port)
    try { $listener.Start() } catch { throw "El puerto $port está ocupado. No se inició ninguna instancia adicional." } finally { $listener.Stop() }
}
Push-Location $projectRoot
try {
    dotnet build backend/Cash.sln --configuration Release
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo compilar el backend.' }
    if (-not (Test-Path -LiteralPath 'frontend/node_modules')) {
        npm ci --prefix frontend
        if ($LASTEXITCODE -ne 0) { throw 'No se pudieron instalar las dependencias.' }
    }
    $env:VITE_API_URL = "http://127.0.0.1:$ApiPort"
    npm run build --prefix frontend
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo compilar la interfaz.' }
    $env:AllowedOrigins__0 = "http://127.0.0.1:$WebPort"
    $env:Logging__LogLevel__Microsoft = 'Warning'
    $logDir = Join-Path $projectRoot 'work'
    [System.IO.Directory]::CreateDirectory($logDir) | Out-Null
    $apiProcess = Start-Process -FilePath (Get-Command dotnet).Source -WorkingDirectory (Join-Path $projectRoot 'backend/Cash.Api/bin/Release/net10.0') -ArgumentList @('Cash.Api.dll', '--urls', "http://127.0.0.1:$ApiPort") -WindowStyle Hidden -RedirectStandardOutput (Join-Path $logDir 'api.log') -RedirectStandardError (Join-Path $logDir 'api-error.log') -PassThru
    try {
        $healthy = $false
        for ($attempt = 0; $attempt -lt 40; $attempt++) {
            if ($apiProcess.HasExited) { throw 'La API se detuvo. Revise work/api-error.log.' }
            try {
                $health = Invoke-RestMethod -Uri "http://127.0.0.1:$ApiPort/health" -TimeoutSec 2
                if ($health.status -eq 'ok') { $healthy = $true; break }
            } catch [System.Net.WebException] { Write-Verbose "Esperando a la API: intento $attempt" }
            Start-Sleep -Milliseconds 500
        }
        if (-not $healthy) { throw 'La API no pudo conectarse a la base de datos. Revise los registros locales.' }
        $webProcess = Start-Process -FilePath (Get-Command node).Source -WorkingDirectory (Join-Path $projectRoot 'frontend') -ArgumentList @('node_modules/vite/bin/vite.js', 'preview', '--host', '127.0.0.1', '--port', "$WebPort", '--strictPort') -WindowStyle Hidden -RedirectStandardOutput (Join-Path $logDir 'web.log') -RedirectStandardError (Join-Path $logDir 'web-error.log') -PassThru
        Start-Sleep -Seconds 1
        if ($webProcess.HasExited) { throw 'La interfaz no pudo iniciarse. Revise work/web-error.log.' }
        @(
            @{ id = $apiProcess.Id; started = $apiProcess.StartTime.ToUniversalTime().ToString('O'); name = $apiProcess.ProcessName },
            @{ id = $webProcess.Id; started = $webProcess.StartTime.ToUniversalTime().ToString('O'); name = $webProcess.ProcessName }
        ) | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $logDir 'local-processes.json') -Encoding utf8
        Write-Output "Aplicación disponible en http://127.0.0.1:$WebPort"
    } catch {
        if ($webProcess -and -not $webProcess.HasExited) { $webProcess.Kill() }
        if (-not $apiProcess.HasExited) { $apiProcess.Kill() }
        throw
    }
} finally { Pop-Location }
