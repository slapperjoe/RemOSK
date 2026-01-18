Add-Type -AssemblyName System.Drawing
$pngPath = "d:\RemOSK\RemOSK\Resources\Icons\app_icon.png"
$icoPath = "d:\RemOSK\RemOSK\Resources\Icons\app.ico"

if (Test-Path $pngPath) {
    Write-Host "Converting $pngPath to $icoPath..."
    $img = [System.Drawing.Bitmap]::FromFile($pngPath)
    $handle = $img.GetHicon()
    $icon = [System.Drawing.Icon]::FromHandle($handle)
    
    $fs = [System.IO.File]::Open($icoPath, [System.IO.FileMode]::Create)
    $icon.Save($fs)
    $fs.Close()
    
    # Cleanup handles via Win32 if needed, but script exit cleans up
    $icon.Dispose()
    $img.Dispose()
    Write-Host "Conversion Complete."
} else {
    Write-Error "PNG not found at $pngPath"
}
