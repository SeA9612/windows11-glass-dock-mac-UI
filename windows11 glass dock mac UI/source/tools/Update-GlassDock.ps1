$ErrorActionPreference = 'Stop'
$tempPath = $null
try {
    $basePath = Join-Path $PSScriptRoot 'GlassDock.exe'
    $destination = Join-Path $PSScriptRoot 'GlassDock-0.2'
    $outputPath = Join-Path $destination 'GlassDock.exe'
    if (-not (Test-Path -LiteralPath $basePath -PathType Leaf)) {
        throw 'Put these update files beside your existing GlassDock.exe, then run Update-GlassDock.cmd.'
    }
    if (Test-Path -LiteralPath $destination) {
        throw 'GlassDock-0.2 already exists. Rename that folder before retrying. Existing files were not changed.'
    }
    $patchPath = Join-Path $PSScriptRoot 'GlassDock-0.2.delta'
    $patchStream = [System.IO.File]::OpenRead($patchPath)
    $reader = [System.IO.BinaryReader]::new($patchStream)
    try {
        if ([System.Text.Encoding]::ASCII.GetString($reader.ReadBytes(8)) -ne 'GDUPD002') { throw 'Invalid update header.' }
        $baseHash = [BitConverter]::ToString($reader.ReadBytes(32)).Replace('-', '')
        $newHash = [BitConverter]::ToString($reader.ReadBytes(32)).Replace('-', '')
        $expectedSize = $reader.ReadInt64()
        $actualHash = (Get-FileHash -LiteralPath $basePath -Algorithm SHA256).Hash
        if ($actualHash -ne $baseHash) {
            $oldConfig = Join-Path $PSScriptRoot 'GlassDock.runtimeconfig.json'
            $oldAssembly = Join-Path $PSScriptRoot 'GlassDock.dll'
            if ((Test-Path -LiteralPath $oldConfig) -and (Test-Path -LiteralPath $oldAssembly)) {
                New-Item -ItemType Directory -Path $destination | Out-Null
                Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'small') -File | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $destination }
                Write-Host 'SUCCESS: Small-build update created in GlassDock-0.2. Run GlassDock.exe there.' -ForegroundColor Green
                exit 0
            }
            throw 'This is not the original full build. Use the small version download, or the original 0.1 full-build EXE.'
        }
        $tempPath = Join-Path $PSScriptRoot ('GlassDock-update-' + [Guid]::NewGuid().ToString('N') + '.tmp')
        $output = [System.IO.File]::Open($tempPath, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write)
        $source = [System.IO.File]::OpenRead($basePath)
        try {
            $buffer = New-Object byte[] 1048576
            while ($true) {
                $operation = $reader.ReadByte()
                if ($operation -eq 0) { break }
                if ($operation -eq 1) {
                    $offset = $reader.ReadInt64()
                    $remaining = $reader.ReadInt64()
                    if ($offset -lt 0 -or $remaining -lt 0 -or $offset -gt ($source.Length - $remaining)) { throw 'Invalid copy instruction.' }
                    $source.Position = $offset
                    while ($remaining -gt 0) {
                        $take = [int][Math]::Min([long]$buffer.Length, $remaining)
                        $read = $source.Read($buffer, 0, $take)
                        if ($read -le 0) { throw 'Unexpected end of original file.' }
                        $output.Write($buffer, 0, $read)
                        $remaining -= $read
                    }
                } elseif ($operation -eq 2) {
                    $remaining = $reader.ReadInt64()
                    if ($remaining -lt 0 -or $remaining -gt ($expectedSize - $output.Length)) { throw 'Invalid data instruction.' }
                    while ($remaining -gt 0) {
                        $take = [int][Math]::Min([long]$buffer.Length, $remaining)
                        $read = $reader.Read($buffer, 0, $take)
                        if ($read -le 0) { throw 'Incomplete update file.' }
                        $output.Write($buffer, 0, $read)
                        $remaining -= $read
                    }
                } else { throw 'Unknown update instruction.' }
                if ($output.Length -gt $expectedSize) { throw 'Update exceeds expected size.' }
            }
            if ($output.Length -ne $expectedSize) { throw 'Output size mismatch.' }
        } finally {
            $source.Dispose()
            $output.Dispose()
        }
        if ((Get-FileHash -LiteralPath $tempPath -Algorithm SHA256).Hash -ne $newHash) { throw 'SHA256 verification failed. Original file was not changed.' }
        New-Item -ItemType Directory -Path $destination | Out-Null
        [System.IO.File]::Move($tempPath, $outputPath)
        Write-Host 'SUCCESS: Full-build update verified. Run GlassDock-0.2\GlassDock.exe.' -ForegroundColor Green
        Write-Host 'No separate runtime installation is required. The old EXE was not changed.'
    } finally {
        $reader.Dispose()
        $patchStream.Dispose()
    }
} catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
} finally {
    if ($null -ne $tempPath -and (Test-Path -LiteralPath $tempPath)) { Remove-Item -LiteralPath $tempPath }
}
