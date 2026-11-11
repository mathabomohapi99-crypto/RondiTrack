# usage (PowerShell, from the repo root):  . .\scripts\e2e-env.ps1
function New-Secret([int]$bytes) { [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes($bytes)) }
$env:E2E_DB_PASSWORD = "db-" + (New-Secret 12).Replace('/', 'x').Replace('+', 'y')
$env:E2E_PASSWORD = "E2e-" + (New-Secret 12).Replace('/', 'x').Replace('+', 'y')
$env:JWT_SIGNING_KEY = New-Secret 48
$env:SESSION_SECRET = New-Secret 32
$env:API_BASE_URL = "http://localhost:5134"
Write-Host "Throwaway e2e values set for this terminal only."