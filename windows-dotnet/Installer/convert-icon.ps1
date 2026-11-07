Add-Type -AssemblyName System.Drawing
$assets=Join-Path $PSScriptRoot '../UI/assets'
$image=[Drawing.Image]::FromFile((Join-Path $assets 'AppIcon.png'))
$images=@()
try{
 foreach($size in @(16,24,32,48,64,128,256)){
  $bitmap=[Drawing.Bitmap]::new($size,$size,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $graphics=[Drawing.Graphics]::FromImage($bitmap)
  $graphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  # Normalize only the Windows icon canvas. Keep the app's PNG and bear artwork unchanged.
  $graphics.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::HighQuality
  $graphics.Clear([Drawing.Color]::Transparent)
  $graphics.DrawImage($image,[Drawing.Rectangle]::new(0,0,$size,$size),46,49,1160,1160,[Drawing.GraphicsUnit]::Pixel)
  $stream=[IO.MemoryStream]::new()
  $bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
  $images+=@{Size=$size;Data=$stream.ToArray()}
  $graphics.Dispose();$bitmap.Dispose();$stream.Dispose()
 }
 $writer=[IO.BinaryWriter]::new([IO.File]::Create((Join-Path $assets 'AppIcon.ico')))
 try{
  $writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$images.Count)
  $offset=6+16*$images.Count
  foreach($icon in $images){$dimension=if($icon.Size -eq 256){0}else{$icon.Size};$writer.Write([byte]$dimension);$writer.Write([byte]$dimension);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$icon.Data.Length);$writer.Write([uint32]$offset);$offset+=$icon.Data.Length}
  foreach($icon in $images){$writer.Write([byte[]]$icon.Data)}
 }finally{$writer.Dispose()}
}finally{$image.Dispose()}
Write-Output 'Transparent multi-resolution Windows icon created.'
