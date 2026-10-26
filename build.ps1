$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
  New-Item -ItemType Directory -Path package -Force | Out-Null
  Push-Location ui
  try { npm run build; if ($LASTEXITCODE -ne 0) { throw 'UI build failed' } } finally { Pop-Location }
  dotnet build src/ApartmentPlan.csproj -c Release --nologo
  if ($LASTEXITCODE -ne 0) { throw 'SDK build failed' }
  foreach ($name in @('ApartmentPlan.dll','ApartmentPlan_win_x86_64.dll')) {
    Copy-Item -LiteralPath "src/bin/Release/net48/$name" -Destination package -Force
  }
  Copy-Item lang package -Recurse -Force
  Copy-Item README.md,NOTICE.md,TRANSLATING.md package -Force
} finally { Pop-Location }