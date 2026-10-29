<#
    Impact Nexus - setup and run
    ----------------------------
    Double-click Setup-and-Run.cmd instead of running this file directly; the
    .cmd wrapper bypasses the PowerShell execution policy for you.

    What this script does:
      1. checks that the .NET 10 SDK is installed
      2. finds a SQL Server instance (SQL Server Express, or LocalDB)
      3. points the application at it through an environment variable, so no
         file in the project has to be edited
      4. restores, builds and starts the application
      5. opens the browser when the site answers

    The application creates its own database on first launch and fills it with
    demo data. Nothing else has to be run by hand.
#>

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

function Step($m) {
    Write-Host ''
    Write-Host "  $m" -ForegroundColor Cyan
}
function Ok($m)   { Write-Host "    [ok]   $m" -ForegroundColor Green }
function Info($m) { Write-Host "           $m" -ForegroundColor Gray }
function Warn($m) { Write-Host "    [warn] $m" -ForegroundColor Yellow }
function Fail($m) { Write-Host "    [stop] $m" -ForegroundColor Red }

Write-Host ''
Write-Host '  ============================================' -ForegroundColor DarkCyan
Write-Host '   IMPACT NEXUS  -  CSR & CSI platform' -ForegroundColor White
Write-Host '   setup and run' -ForegroundColor DarkGray
Write-Host '  ============================================' -ForegroundColor DarkCyan

# --------------------------------------------------------------------------
Step '1/4  Looking for the .NET 10 SDK'

$dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnetCmd) {
    Fail 'The dotnet command was not found.'
    Info 'Install the .NET 10 SDK from https://dotnet.microsoft.com/download/dotnet/10.0'
    Info 'Then close this window, open a new one and run Setup-and-Run.cmd again.'
    exit 1
}

try { $sdks = & dotnet --list-sdks 2>$null } catch { $sdks = @() }
$sdk10 = $sdks | Where-Object { $_ -match '^10\.' } | Select-Object -First 1

if (-not $sdk10) {
    Fail 'The .NET 10 SDK is not installed (this project targets .NET 10).'
    if ($sdks) {
        Info 'Installed SDKs:'
        $sdks | ForEach-Object { Info ("  " + $_) }
    }
    Info 'Download: https://dotnet.microsoft.com/download/dotnet/10.0'
    exit 1
}
Ok $sdk10.Trim()

# --------------------------------------------------------------------------
Step '2/4  Looking for SQL Server'

$server = $null
$label = $null

$express = Get-Service -Name 'MSSQL$SQLEXPRESS' -ErrorAction SilentlyContinue
if ($express) {
    if ($express.Status -ne 'Running') {
        Info 'SQL Server Express is installed but stopped. Starting it...'
        try {
            Start-Service -Name 'MSSQL$SQLEXPRESS'
            Start-Sleep -Seconds 3
            Ok 'SQL Server Express started.'
        } catch {
            Warn 'Could not start the service automatically (it may need administrator rights).'
            Info 'Open Services, start "SQL Server (SQLEXPRESS)", then run this again.'
            exit 1
        }
    }
    $server = '.\SQLEXPRESS'
    $label = 'SQL Server Express  (.\SQLEXPRESS)'
}

if (-not $server) {
    $localdb = $null
    $roots = @("$env:ProgramFiles\Microsoft SQL Server", "${env:ProgramFiles(x86)}\Microsoft SQL Server")
    foreach ($r in $roots) {
        if (-not (Test-Path $r)) { continue }
        $found = Get-ChildItem -Path $r -Directory -ErrorAction SilentlyContinue |
            ForEach-Object { Join-Path $_.FullName 'Tools\Binn\SqlLocalDB.exe' } |
            Where-Object { Test-Path $_ } |
            Select-Object -First 1
        if ($found) { $localdb = $found; break }
    }
    if (-not $localdb) {
        $cmd = Get-Command sqllocaldb -ErrorAction SilentlyContinue
        if ($cmd) { $localdb = $cmd.Source }
    }

    if ($localdb) {
        try {
            $null = & $localdb start MSSQLLocalDB 2>&1
            $state = (& $localdb info MSSQLLocalDB 2>&1) -join ' '
            if ($state -match 'Running|Stopped') {
                $server = '(localdb)\MSSQLLocalDB'
                $label = 'SQL Server LocalDB  ((localdb)\MSSQLLocalDB)'
            }
        } catch { }
    }
}

if (-not $server) {
    Fail 'No SQL Server instance was found.'
    Info 'Install one of these, then run this again:'
    Info '  SQL Server Express (free): https://www.microsoft.com/sql-server/sql-server-downloads'
    Info '  or Visual Studio, which installs SQL Server LocalDB with it.'
    exit 1
}
Ok $label

# --------------------------------------------------------------------------
Step '3/4  Pointing the application at the database'

$connection = "Server=$server;Database=ImpactNexus;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
$env:ConnectionStrings__DefaultConnection = $connection
$env:Database__SeedDemoData = 'true'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
Ok 'Connection settings applied for this session only.'
Info 'No project file was modified.'

# pick a free port
function Test-PortBusy([int]$p) {
    try {
        $client = New-Object Net.Sockets.TcpClient
        $client.Connect('127.0.0.1', $p)
        $client.Close()
        return $true
    } catch { return $false }
}

$port = 5199
while ($port -lt 5300 -and (Test-PortBusy $port)) { $port++ }
$url = "http://localhost:$port"

Write-Host ''
Write-Host '  Demo accounts' -ForegroundColor White
Write-Host '    IT Administrator      admin@impactnexus.local        Admin#2026' -ForegroundColor Gray
Write-Host '    CSR Manager (BMW)     csr.manager@impactnexus.local  Manager#2026' -ForegroundColor Gray
Write-Host '    Investor              investor@impactnexus.local     Investor#2026' -ForegroundColor Gray
Write-Host '    Community / Volunteer community@impactnexus.local    Volunteer#2026' -ForegroundColor Gray

# --------------------------------------------------------------------------
Step '4/4  Building and starting the application'
Info 'The first launch takes two to five minutes: it restores packages, builds,'
Info 'creates the database and fills it with demo data. Later launches are quick.'
Write-Host ''

& dotnet restore "ImpactNexus.sln" --nologo
if ($LASTEXITCODE -ne 0) {
    Fail 'Package restore failed. Scroll up for the first error.'
    exit 1
}

$browserJob = Start-Job -ScriptBlock {
    param($u, $p)
    for ($i = 0; $i -lt 150; $i++) {
        Start-Sleep -Seconds 2
        try {
            $c = New-Object Net.Sockets.TcpClient
            $c.Connect('127.0.0.1', $p)
            $c.Close()
            Start-Process $u
            return
        } catch { }
    }
} -ArgumentList $url, $port

Write-Host ''
Write-Host "  The application will open at $url" -ForegroundColor Green
Write-Host '  Press Ctrl+C in this window to stop it.' -ForegroundColor DarkGray
Write-Host ''

try {
    & dotnet run --project 'src\ImpactNexus.Web' --no-launch-profile --urls $url
} finally {
    Stop-Job $browserJob -ErrorAction SilentlyContinue
    Remove-Job $browserJob -Force -ErrorAction SilentlyContinue
    Write-Host ''
    Write-Host '  The application has stopped.' -ForegroundColor DarkGray
}
