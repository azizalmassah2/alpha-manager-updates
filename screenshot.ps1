Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Start-Sleep -Seconds 3
$Screen = [System.Windows.Forms.SystemInformation]::VirtualScreen
$Bitmap = New-Object System.Drawing.Bitmap $Screen.Width, $Screen.Height
$Graphics = [System.Drawing.Graphics]::FromImage($Bitmap)
$Graphics.CopyFromScreen($Screen.Left, $Screen.Top, 0, 0, $Bitmap.Size)
$Bitmap.Save("C:\Users\MrAziz\.gemini\antigravity\brain\6bb8795a-6087-4e0f-984f-b7e2636f66c8\screenshot.png")
