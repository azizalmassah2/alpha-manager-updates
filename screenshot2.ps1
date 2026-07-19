Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Start-Sleep -Seconds 30
$Screen = [System.Windows.Forms.SystemInformation]::VirtualScreen
$Bitmap = New-Object System.Drawing.Bitmap $Screen.Width, $Screen.Height
$Graphics = [System.Drawing.Graphics]::FromImage($Bitmap)
$Graphics.CopyFromScreen($Screen.Left, $Screen.Top, 0, 0, $Bitmap.Size)
$Bitmap.Save("C:\Users\MrAziz\.gemini\antigravity\brain\60da867a-9407-426e-9fce-4fef08391c75\screenshot2.png")
