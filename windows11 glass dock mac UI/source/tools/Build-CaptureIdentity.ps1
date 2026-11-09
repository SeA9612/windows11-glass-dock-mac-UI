param([Parameter(Mandatory=$true)][string]$MakeAppx,[Parameter(Mandatory=$true)][string]$PublishDirectory)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$identityRoot=Join-Path $projectRoot 'packaging\identity'
$target=(Resolve-Path -LiteralPath $PublishDirectory).Path
if(!(Test-Path -LiteralPath (Join-Path $target 'GlassDock.exe'))){throw 'Publish GlassDock.exe before building identity.'}
Copy-Item -LiteralPath (Join-Path $identityRoot 'Assets') -Destination $target -Recurse -Force
$package=Join-Path $target 'GlassDock.LocalPreview.identity.msix'
# External executable paths intentionally live outside the identity-only package.
& $MakeAppx pack /o /d $identityRoot /nv /p $package
if($LASTEXITCODE -ne 0){throw 'Identity package build failed.'}
Write-Output $package
