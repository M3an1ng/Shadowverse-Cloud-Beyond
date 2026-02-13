$cs = @"
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

public static class AlphaPngEstimator {
    public static bool HasNonOpaqueAlpha(Bitmap bmp) {
        int w = bmp.Width;
        int h = bmp.Height;
        using (var normalized = new Bitmap(w, h, PixelFormat.Format32bppArgb)) {
            using (var g = Graphics.FromImage(normalized)) {
                g.DrawImage(bmp, 0, 0, w, h);
            }
            var rect = new Rectangle(0, 0, w, h);
            var data = normalized.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try {
                int bytes = Math.Abs(data.Stride) * h;
                byte[] buffer = new byte[bytes];
                Marshal.Copy(data.Scan0, buffer, 0, bytes);
                int stride = Math.Abs(data.Stride);
                for (int y = 0; y < h; y++) {
                    int row = y * stride;
                    for (int x = 0; x < w; x++) {
                        byte a = buffer[row + x * 4 + 3];
                        if (a != 255) return true;
                    }
                }
                return false;
            } finally {
                normalized.UnlockBits(data);
            }
        }
    }

    public static long EstimateResizedPngSize(Bitmap source, int targetW, int targetH) {
        using (var resized = new Bitmap(targetW, targetH, PixelFormat.Format32bppArgb)) {
            using (var g = Graphics.FromImage(resized)) {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);
                g.DrawImage(source, 0, 0, targetW, targetH);
            }
            using (var ms = new MemoryStream()) {
                resized.Save(ms, ImageFormat.Png);
                return ms.Length;
            }
        }
    }
}
"@
Add-Type -TypeDefinition $cs -ReferencedAssemblies "System.Drawing"

$files = Get-ChildItem assets/cards -Recurse -File -ErrorAction SilentlyContinue | Where-Object { $_.Name -match '(?i)^preview\.(png|jpg|jpeg)$' }
if (-not $files) { 'no preview files found'; exit 0 }

$rows = New-Object System.Collections.Generic.List[object]
foreach($f in $files){
    try {
        $bmp = [System.Drawing.Bitmap]::new($f.FullName)
        $hasAlpha = [AlphaPngEstimator]::HasNonOpaqueAlpha($bmp)
        if ($hasAlpha) {
            $w = $bmp.Width; $h = $bmp.Height
            $t1w = [Math]::Min($w, 424)
            $t1h = [int][Math]::Round($h * ($t1w / [double]$w))
            $s1 = [AlphaPngEstimator]::EstimateResizedPngSize($bmp, $t1w, $t1h)
            $rows.Add([pscustomobject]@{ path=$f.FullName; orig=[long]$f.Length; s424png=[long]$s1 })
        }
        $bmp.Dispose()
    } catch {}
}

function SumField($arr, $name){ if(-not $arr -or $arr.Count -eq 0){ return 0L }; return [long](($arr | Measure-Object -Property $name -Sum).Sum) }
$orig = SumField $rows 'orig'
$s424 = SumField $rows 's424png'
"alpha_count=$($rows.Count)"
"orig_mb=$([math]::Round($orig/1MB,2))"
"s424_png_mb=$([math]::Round($s424/1MB,2)) save_pct=$([math]::Round((1-($s424/[double]$orig))*100,2))"
