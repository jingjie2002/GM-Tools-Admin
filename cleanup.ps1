<# 
    GameAdmin Process Cleanup Script
    Purpose: Kill stale processes by name and port
    Usage: .\cleanup.ps1
#>

Write-Host "=== GameAdmin Process Cleanup ===" -ForegroundColor Cyan

# 1. Kill by process name
try {
    $byName = Get-Process -Name "GameAdmin.Api" -ErrorAction SilentlyContinue
    if ($byName) {
        Write-Host "[INFO] Found $($byName.Count) process(es) by name" -ForegroundColor Yellow
        $byName | ForEach-Object { 
            Write-Host "  -> Killing PID: $($_.Id)" -ForegroundColor Red
            Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
        }
    } else {
        Write-Host "[INFO] No 'GameAdmin.Api' process found" -ForegroundColor Green
    }
} catch {
    Write-Host "[WARN] Error checking processes by name" -ForegroundColor Yellow
}

# 2. Kill by port (5201)
try {
    $port = 5201
    $byPort = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue | 
              Where-Object { $_.State -eq 'Listen' } |
              Select-Object -ExpandProperty OwningProcess -Unique

    if ($byPort) {
        Write-Host "[INFO] Found process(es) on port $port" -ForegroundColor Yellow
        $byPort | ForEach-Object {
            $proc = Get-Process -Id $_ -ErrorAction SilentlyContinue
            if ($proc) {
                Write-Host "  -> Killing PID: $_ ($($proc.ProcessName))" -ForegroundColor Red
                Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue
            }
        }
    } else {
        Write-Host "[INFO] Port $port is free" -ForegroundColor Green
    }
} catch {
    Write-Host "[WARN] Error checking port $port" -ForegroundColor Yellow
}

Write-Host "=== Cleanup Complete ===" -ForegroundColor Cyan
