using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

// ZoneId-Cleaner.ico と preview.png を生成する（make-icon.bat から実行）
static class MakeIcon
{
    static Bitmap Frame(int S)
    {
        var bmp = new Bitmap(S, S, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);
            float u = S / 256f;
            Func<float, float, PointF> P = (x, y) => new PointF(x * u, y * u);

            // 書類本体（右上を折り返し）
            var page = new[] { P(44, 14), P(156, 14), P(212, 70), P(212, 242), P(44, 242) };
            using (var br = new LinearGradientBrush(P(44, 14), P(212, 242), Color.White, Color.FromArgb(205, 220, 240)))
                g.FillPolygon(br, page);
            using (var pen = new Pen(Color.FromArgb(70, 100, 150), Math.Max(1f, 8 * u)) { LineJoin = LineJoin.Round })
            {
                g.DrawPolygon(pen, page);
                var fold = new[] { P(156, 14), P(156, 70), P(212, 70) };
                using (var fb = new SolidBrush(Color.FromArgb(150, 175, 215))) g.FillPolygon(fb, fold);
                g.DrawPolygon(pen, fold);
            }

            // オレンジの Z（Zone の印）
            using (var font = new Font("Arial Black", 104 * u, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (var zb = new SolidBrush(Color.FromArgb(240, 130, 20)))
                g.DrawString("Z", font, zb, new RectangleF(44 * u, 80 * u, 168 * u, 120 * u), sf);

            // 右下の緑のチェックバッジ
            float cx = 190 * u, cy = 190 * u, r = 58 * u;
            using (var gb = new SolidBrush(Color.FromArgb(34, 160, 80)))
                g.FillEllipse(gb, cx - r, cy - r, 2 * r, 2 * r);
            using (var wp = new Pen(Color.White, Math.Max(1.5f, 16 * u)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                g.DrawLines(wp, new[] { P(162, 192), P(182, 212), P(220, 168) });
        }
        return bmp;
    }

    static void Main()
    {
        int[] sizes = { 16, 24, 32, 48, 64, 128, 256 };
        var pngs = new byte[sizes.Length][];
        for (int i = 0; i < sizes.Length; i++)
        {
            using (var b = Frame(sizes[i]))
            using (var ms = new MemoryStream())
            {
                b.Save(ms, ImageFormat.Png);
                pngs[i] = ms.ToArray();
                if (sizes[i] == 256) b.Save("preview.png", ImageFormat.Png);
            }
        }

        // PNG 埋め込み形式の ICO
        using (var fs = File.Create("ZoneId-Cleaner.ico"))
        using (var w = new BinaryWriter(fs))
        {
            w.Write((ushort)0); w.Write((ushort)1); w.Write((ushort)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                w.Write((byte)(sizes[i] % 256)); w.Write((byte)(sizes[i] % 256));
                w.Write((byte)0); w.Write((byte)0);
                w.Write((ushort)1); w.Write((ushort)32);
                w.Write((uint)pngs[i].Length); w.Write((uint)offset);
                offset += pngs[i].Length;
            }
            foreach (var p in pngs) w.Write(p);
        }
    }
}
