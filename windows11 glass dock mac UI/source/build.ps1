$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    dotnet run --project tests/PolicyTests.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw '클릭 정책 테스트 실패' }
    dotnet publish src/Dock/Dock.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts/win-x64
    if ($LASTEXITCODE -ne 0) { throw 'Windows 빌드 실패' }
} finally { Pop-Location }
