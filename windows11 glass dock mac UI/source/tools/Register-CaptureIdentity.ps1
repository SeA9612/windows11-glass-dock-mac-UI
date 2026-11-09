param([Parameter(Mandatory=$true)][string]$PublishDirectory,[switch]$TrustLocalSigningCertificate)
$ErrorActionPreference='Stop'
$target=(Resolve-Path -LiteralPath $PublishDirectory).Path
$package=Join-Path $target 'GlassDock.LocalPreview.identity.msix'
if(!(Test-Path -LiteralPath $package)){throw 'Build the identity package first.'}
$certificateFile=Join-Path $target 'GlassDock.Local.cer'
$certificate=[System.Security.Cryptography.X509Certificates.X509Certificate2]::new($certificateFile)
if($certificate.Subject -ne 'CN=GlassDock Local'){throw 'Unexpected identity package signer.'}
if(!(Test-Path -LiteralPath (Join-Path 'Cert:\LocalMachine\TrustedPeople' $certificate.Thumbprint)))
{
    if(!$TrustLocalSigningCertificate){throw 'User approval and administrator access are required for the local GlassDock signing certificate. Registration is currently deferred.'}
    Import-Certificate -FilePath $certificateFile -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null
}
Add-AppxPackage -Path $package -ExternalLocation $target -ErrorAction Stop
Get-AppxPackage -Name 'GlassDock.LocalPreview' | Select-Object Name,PackageFullName,Status
