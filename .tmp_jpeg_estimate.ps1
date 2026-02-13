$cs = @"
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

public static class ImgCompressEstimator {
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

    public static long EstimateJpegSize(Bitmap source, long quality) {
        var codec = GetJpegCodec();
        if (codec == null) return -1;
        using (var flattened = new Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb)) {
            using (var g = Graphics.FromImage(flattened)) {
                g.Clear(Color.Black);
                g.DrawImage(source, 0, 0, source.Width, source.Height);
            }
            var p = new EncoderParameters(1);
            p.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
            using (var ms = new MemoryStream()) {
                flattened.Save(ms, codec, p);
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
        $hasAlpha = [ImgCompressEstimator]::HasNonOpaqueAlpha($bmp)
        $q90 = [ImgCompressEstimator]::EstimateJpegSize($bmp, 90)
        $q85 = [ImgCompressEstimator]::EstimateJpegSize($bmp, 85)
        $q80 = [ImgCompressEstimator]::EstimateJpegSize($bmp, 80)
        $bmp.Dispose()
        $rows.Add([pscustomobject]@{
            path = $f.FullName
            orig = [long]$f.Length
            hasAlpha = $hasAlpha
            q90 = [long]$q90
            q85 = [long]$q85
            q80 = [long]$q80
        })
    } catch {
        Write-Output "ERR $($f.FullName) $($_.Exception.Message)"
    }
}

$opaque = @($rows | Where-Object { -not $_.hasAlpha })
$alpha = @($rows | Where-Object { $_.hasAlpha })

function SumField($arr, $name){ if(-not $arr -or $arr.Count -eq 0){ return 0L }; return [long](($arr | Measure-Object -Property $name -Sum).Sum) }

$origOpaque = SumField $opaque 'orig'
$q90Opaque = SumField $opaque 'q90'
$q85Opaque = SumField $opaque 'q85'
$q80Opaque = SumField $opaque 'q80'

"opaque_count=$($opaque.Count) orig_mb=$([math]::Round($origOpaque/1MB,2))"
"q90_mb=$([math]::Round($q90Opaque/1MB,2)) save_pct=$([math]::Round((1-($q90Opaque/[double]$origOpaque))*100,2))"
"q85_mb=$([math]::Round($q85Opaque/1MB,2)) save_pct=$([math]::Round((1-($q85Opaque/[double]$origOpaque))*100,2))"
"q80_mb=$([math]::Round($q80Opaque/1MB,2)) save_pct=$([math]::Round((1-($q80Opaque/[double]$origOpaque))*100,2))"

$top = $opaque | Sort-Object orig -Descending | Select-Object -First 20 path,orig,q85
$top | ForEach-Object {
  "TOP|$($_.path)|orig_kb=$([math]::Round($_.orig/1KB,1))|q85_kb=$([math]::Round($_.q85/1KB,1))|save_pct=$([math]::Round((1-($_.q85/[double]$_.orig))*100,2))"
}

"alpha_count=$($alpha.Count) alpha_mb=$([math]::Round((SumField $alpha 'orig')/1MB,2))"
