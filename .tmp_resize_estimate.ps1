$cs = @"
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

public static class ImgResizeEstimator {
    static ImageCodecInfo GetJpegCodec() {
        foreach (var c in ImageCodecInfo.GetImageEncoders()) {
            if (c.MimeType == "image/jpeg") return c;
        }
        return null;
    }

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

    public static long EstimateResizedJpegSize(Bitmap source, int targetW, int targetH, long quality) {
        var codec = GetJpegCodec();
        if (codec == null) return -1;
        using (var resized = new Bitmap(targetW, targetH, PixelFormat.Format24bppRgb)) {
            using (var g = Graphics.FromImage(resized)) {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.Clear(Color.Black);
                g.DrawImage(source, 0, 0, targetW, targetH);
            }
            var p = new EncoderParameters(1);
            p.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
            using (var ms = new MemoryStream()) {
                resized.Save(ms, codec, p);
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
        $hasAlpha = [ImgResizeEstimator]::HasNonOpaqueAlpha($bmp)
        $w = $bmp.Width; $h = $bmp.Height
        $t1w = [Math]::Min($w, 424)
        $t1h = [int][Math]::Round($h * ($t1w / [double]$w))
        $t2w = [Math]::Min($w, 320)
        $t2h = [int][Math]::Round($h * ($t2w / [double]$w))
        $s1 = [ImgResizeEstimator]::EstimateResizedJpegSize($bmp, $t1w, $t1h, 85)
        $s2 = [ImgResizeEstimator]::EstimateResizedJpegSize($bmp, $t2w, $t2h, 82)
        $bmp.Dispose()

        $rows.Add([pscustomobject]@{ path=$f.FullName; orig=[long]$f.Length; hasAlpha=$hasAlpha; s424=[long]$s1; s320=[long]$s2 })
    } catch {
        Write-Output "ERR $($f.FullName) $($_.Exception.Message)"
    }
}

$opaque = @($rows | Where-Object { -not $_.hasAlpha })
function SumField($arr, $name){ if(-not $arr -or $arr.Count -eq 0){ return 0L }; return [long](($arr | Measure-Object -Property $name -Sum).Sum) }
$orig = SumField $opaque 'orig'
$s424 = SumField $opaque 's424'
$s320 = SumField $opaque 's320'
"opaque_count=$($opaque.Count)"
"orig_mb=$([math]::Round($orig/1MB,2))"
"s424_q85_mb=$([math]::Round($s424/1MB,2)) save_pct=$([math]::Round((1-($s424/[double]$orig))*100,2))"
"s320_q82_mb=$([math]::Round($s320/1MB,2)) save_pct=$([math]::Round((1-($s320/[double]$orig))*100,2))"
