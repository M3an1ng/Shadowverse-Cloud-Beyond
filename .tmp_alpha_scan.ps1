$cs = @"
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class ImgAlphaUtil {
    public static string Analyze(string path) {
        using (var bmp = new Bitmap(path)) {
            int w = bmp.Width;
            int h = bmp.Height;
            var pf = bmp.PixelFormat;
            bool hasAlpha = Image.IsAlphaPixelFormat(pf);
            bool hasNonOpaqueAlpha = false;
            byte minA = 255;
            byte maxA = 0;

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
                            if (a < minA) minA = a;
                            if (a > maxA) maxA = a;
                            if (a != 255) hasNonOpaqueAlpha = true;
                        }
                    }
                } finally {
                    normalized.UnlockBits(data);
                }
            }

            long size = new System.IO.FileInfo(path).Length;
            return string.Format("{0}|{1}|{2}x{3}|{4}|{5}|{6}|{7}", path, size, w, h, pf, hasAlpha, minA, hasNonOpaqueAlpha);
        }
    }
}
"@

Add-Type -TypeDefinition $cs -ReferencedAssemblies "System.Drawing"

$files = Get-ChildItem assets/cards -Recurse -File -ErrorAction SilentlyContinue | Where-Object { $_.Name -match '(?i)^preview\.(png|jpg|jpeg)$' }
if (-not $files) { 'no preview files found'; exit 0 }

$out = New-Object System.Collections.Generic.List[string]
foreach($f in $files){
    try { $out.Add([ImgAlphaUtil]::Analyze($f.FullName)) }
    catch { $out.Add("ERR|$($f.FullName)|$($_.Exception.Message)") }
}
$out | Set-Content .tmp_preview_alpha_report.txt -Encoding UTF8

$rows = $out | Where-Object { $_ -notmatch '^ERR\|' } | ForEach-Object {
    $p = $_ -split '\|'
    [pscustomobject]@{
        path = $p[0]
        size = [long]$p[1]
        wh = $p[2]
        pf = $p[3]
        hasAlphaFormat = [bool]::Parse($p[4])
        minAlpha = [int]$p[5]
        hasNonOpaqueAlpha = [bool]::Parse($p[6])
        ext = [System.IO.Path]::GetExtension($p[0]).ToLowerInvariant()
    }
}

"rows=$($rows.Count)"
$rows | Group-Object ext | ForEach-Object { "ext=$($_.Name) count=$($_.Count) total_mb=$([math]::Round((($_.Group | Measure-Object size -Sum).Sum)/1MB,2))" }
"nonOpaque_count=$((@($rows | Where-Object {$_.hasNonOpaqueAlpha}).Count))"
"nonOpaque_mb=$([math]::Round(((@($rows | Where-Object {$_.hasNonOpaqueAlpha} | Measure-Object size -Sum).Sum))/1MB,2))"
"opaque_count=$((@($rows | Where-Object {-not $_.hasNonOpaqueAlpha}).Count))"
"opaque_mb=$([math]::Round(((@($rows | Where-Object {-not $_.hasNonOpaqueAlpha} | Measure-Object size -Sum).Sum))/1MB,2))"
$rows | Group-Object wh | Sort-Object Count -Descending | Select-Object -First 10 | ForEach-Object { "size=$($_.Name) count=$($_.Count)" }
