using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Windows.Forms;

// .NET Framework 4.6.2 以降として動作させ、260 文字以上のパス(UNC 含む)を
// Path.GetFullPath / Directory.GetFiles などで扱えるようにする
// (この指定がないと旧来の MAX_PATH 制限の動作になる)
[assembly: TargetFramework(".NETFramework,Version=v4.7.2")]

// Zone.Identifier 一括解除ツール (GUI版)
// - exe アイコンへのドロップ（コマンドライン引数）とウィンドウへのドロップの両方に対応
static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm(args));
    }
}

static class ZoneUtil
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool DeleteFile(string p);

    public enum Result { Success, Failure, Skipped }

    // 絶対パスに直し、\\?\ 付きの拡張パスにする(260 文字以上のパスや UNC パス用)
    // 列挙・存在確認・削除のすべてをこの形式で行えば、OS の長いパス設定に依存しない
    public static string ToExtended(string path)
    {
        string full = Path.GetFullPath(path);
        if (full.StartsWith(@"\\?\")) return full;
        if (full.StartsWith(@"\\")) return @"\\?\UNC\" + full.Substring(2);
        return @"\\?\" + full;
    }

    // 表示用に \\?\ を取り除く
    public static string ToDisplay(string path)
    {
        if (path.StartsWith(@"\\?\UNC\")) return @"\\" + path.Substring(8);
        if (path.StartsWith(@"\\?\")) return path.Substring(4);
        return path;
    }

    // 戻り値: 結果と表示用メッセージ
    public static Result Remove(string path, out string message)
    {
        string prefixed;
        try { prefixed = ToExtended(path); }
        catch (Exception ex)
        {
            message = "[解除失敗] " + ToDisplay(path) + " (" + ex.Message + ")";
            return Result.Failure;
        }
        string full = ToDisplay(prefixed);

        if (DeleteFile(prefixed + ":Zone.Identifier"))
        {
            message = "[解除成功] " + full;
            return Result.Success;
        }

        int code = Marshal.GetLastWin32Error();
        // ファイルまたはストリームが存在しない = 解除不要
        if (code == 2 || code == 3)
        {
            message = "[解除不要] " + full;
            return Result.Skipped;
        }
        message = "[解除失敗] " + full + " (エラー " + code + ": " + new Win32Exception(code).Message + ")";
        return Result.Failure;
    }
}

class MainForm : Form
{
    readonly RichTextBox log = new RichTextBox();
    readonly Label hint = new Label();
    bool showingHint;
    readonly Queue<string> queue = new Queue<string>();
    bool running;
    volatile bool cancel;
    readonly FontFamily uiFontFamily;
    readonly float uiFontPt;   // Windows の UI フォントの大きさ(pt)
    readonly float systemDpi;  // システム DPI(Font のピクセル数の基準になる)
    int success, failure, skipped;
    readonly Queue<KeyValuePair<string, Color>> pending = new Queue<KeyValuePair<string, Color>>();
    readonly Button bCancel = new Button { Text = "中断", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Enabled = false };
    int currentDpi = 96;       // 現在 ApplyDpi で反映している DPI
    readonly FlowLayoutPanel buttons = new FlowLayoutPanel();
    readonly Panel logHost = new Panel();

    public MainForm(string[] args)
    {
        // 高 DPI 対応: マニフェストで DPI 対応(PerMonitorV2)を宣言し、
        // 大きさ・余白・フォントは WinForms の自動拡大に頼らず、ウィンドウの実際の DPI から ApplyDpi で決める
        // (自動拡大はシステム DPI を基準にするため、DPI の異なるモニターで起動・移動すると大きさが狂う)
        SuspendLayout();
        Text = "ZoneId 一括解除ツール";
        // Windows の UI フォント設定に従う。実際の DPI への合わせ込みは ApplyDpi で行う
        uiFontFamily = SystemFonts.MessageBoxFont.FontFamily;
        uiFontPt = SystemFonts.MessageBoxFont.SizeInPoints;
        using (var gr = Graphics.FromHwnd(IntPtr.Zero)) systemDpi = gr.DpiY;
        Font = new Font(uiFontFamily, uiFontPt);
        AllowDrop = true;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        buttons.Dock = DockStyle.Bottom;
        var bFile = new Button { Text = "ファイルを選択...", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        var bFolder = new Button { Text = "フォルダーを選択...", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        var bClear = new Button { Text = "ログをクリア", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        bFile.Click += delegate { PickFiles(); };
        bFolder.Click += delegate { PickFolder(); };
        bClear.Click += delegate { ShowHint(); };
        // 右寄せ（RightToLeft では先に追加したものが右端になる）
        buttons.FlowDirection = FlowDirection.RightToLeft;
        bCancel.Click += delegate { Cancel(); };
        buttons.Controls.AddRange(new Control[] { bClear, bFolder, bFile, bCancel });

        // ログ表示の更新と中断ボタンの有効/無効を、一定間隔でまとめて反映する
        var timer = new System.Windows.Forms.Timer { Interval = 100 };
        timer.Tick += delegate { FlushLog(); };
        timer.Start();


        log.Dock = DockStyle.Fill;
        log.ReadOnly = true;
        log.BackColor = Color.White;
        log.Font = LogFont(uiFontPt);
        log.WordWrap = false;
        log.AllowDrop = true;

        // 操作説明は一覧領域の上に重ねた Label で、上下左右中央に表示する
        hint.Text = "Zone.Identifier を解除したいファイルまたはフォルダーを、\nこのウィンドウにドロップしてください(複数可)。";
        hint.Dock = DockStyle.Fill;
        hint.TextAlign = ContentAlignment.MiddleCenter;
        hint.ForeColor = Color.Gray;
        hint.BackColor = Color.White;
        log.Controls.Add(hint);

        // RichTextBox には Padding がないため、白背景のパネルで包んで余白を作る(余白の大きさは ApplyDpi で決める)
        log.BorderStyle = BorderStyle.None;
        logHost.Dock = DockStyle.Fill;
        logHost.BackColor = Color.White;
        logHost.BorderStyle = BorderStyle.Fixed3D;
        logHost.AllowDrop = true;
        logHost.DragEnter += OnDragEnter;
        logHost.DragDrop += OnDragDrop;
        logHost.Controls.Add(log);

        Controls.Add(logHost);
        Controls.Add(buttons);
        // Dock の重なり順: Fill を最前面側に置かないよう調整
        logHost.BringToFront();

        // ウィンドウ全体（子コントロール含む）でドロップを受け付ける
        foreach (Control c in new Control[] { this, log, hint, buttons })
        {
            c.AllowDrop = true;
            c.DragEnter += OnDragEnter;
            c.DragDrop += OnDragDrop;
        }

        ShowHint();
        ResumeLayout(false);
        PerformLayout();

        // 表示直前: ウィンドウが実際にいるモニターの DPI で、ウィンドウの大きさと各部を決める
        // (Load 時点の DeviceDpi は実際のウィンドウの DPI を返さないことがあるため、ウィンドウに直接問い合わせる)
        Load += delegate
        {
            int dpi = WindowDpi();
            ApplyDpi(dpi);
            ClientSize = new Size(Scale(720, dpi), Scale(460, dpi));
        };

#if TESTDPI
        // 動作確認用(通常のビルドには含まれない):
        // 最初にユーザーが大きさを変えた状態にし、2 秒後に DPI 96 へ、4 秒後に 192 へ移動したものとして WM_DPICHANGED を自分に送る
        // 提示される矩形は、わざと「起動時の大きさ」にしておく(その大きさに戻らないことを確認する)
        var tt = new System.Windows.Forms.Timer { Interval = 2000 };
        int step = 0;
        // 起動時(約 1466x991)より小さく、192dpi の最小サイズ(約 840x600)よりは大きい
        Shown += delegate { Size = new Size(1000, 700); };
        tt.Tick += delegate
        {
            int d = step++ == 0 ? 96 : 192;
            var rc = new RECT { L = Left, T = Top, R = Left + 1466, B = Top + 991 };
            IntPtr p = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(RECT)));
            Marshal.StructureToPtr(rc, p, false);
            SendMessage(Handle, WM_DPICHANGED, (IntPtr)((d << 16) | d), p);
            Marshal.FreeHGlobal(p);
            if (step >= 2) tt.Stop();
        };
        tt.Start();
#endif

        // exe へのドロップ（コマンドライン引数）
        Shown += delegate { if (args.Length > 0) Enqueue(args); };
    }

    // 日本語を含む全行を同じフォントで描くため、日本語対応の等幅フォントを選ぶ
    // (Consolas は日本語の字形がなく、行ごとにフォントが入れ替わってしまう)
    // 大きさは Windows の UI フォントと同じにする
    // (システムフォントに等幅のものはないため、書体だけ等幅フォントにする)
    static Font LogFont(float size)
    {
        try { return new Font(new FontFamily("MS Gothic"), size); }
        catch (ArgumentException) { return new Font(FontFamily.GenericMonospace, size); }
    }

    [DllImport("user32.dll")]
    static extern uint GetDpiForWindow(IntPtr hWnd);

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int L, T, R, B; }

    const int WM_DPICHANGED = 0x02E0;
#if TESTDPI
    [DllImport("user32.dll")]
    static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
#endif

    // DPI の異なるモニターへ移動したとき: ウィンドウの大きさは Windows が提示する矩形に従い、各部は自前で更新する。
    // .NET Framework の WinForms は設定ファイル(.exe.config)で有効にしない限り DpiChanged イベントを発生させないため、
    // メッセージを直接受け取る。WinForms 側の自動拡大には任せない(二重に拡大されるのを避ける)
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_DPICHANGED)
        {
            int dpi = (int)(m.WParam.ToInt64() & 0xFFFF);
            var rc = (RECT)Marshal.PtrToStructure(m.LParam, typeof(RECT));
            if (WindowState == FormWindowState.Normal)
            {
                // 位置は Windows の提示に従い、大きさは「現在の大きさ × DPI の比」にする
                // (ユーザーが変更した大きさを保つ。提示された大きさは使わない)
                // 最小サイズは、一度外してから大きさを決め、その後に新しい DPI の値を設定する。
                // 先に新しい最小サイズを設定すると、小さくしていたウィンドウが最小サイズまで勝手に広がってしまい、
                // 古い最小サイズのままだと、縮小がそこで止まってしまう
                double r = (double)dpi / currentDpi;
                int w = (int)Math.Round(Width * r), h = (int)Math.Round(Height * r);
                MinimumSize = Size.Empty;
                Bounds = new Rectangle(rc.L, rc.T, w, h);
            }
            ApplyDpi(dpi);
            m.Result = IntPtr.Zero;
            return;
        }
        base.WndProc(ref m);
    }

    int WindowDpi()
    {
        try
        {
            uint dpi = GetDpiForWindow(Handle);
            if (dpi != 0) return (int)dpi;
        }
        catch (EntryPointNotFoundException) { } // Windows 10 1607 より前
        return (int)systemDpi;
    }

    // Font は作成時のシステム DPI で文字のピクセル数が決まり、モニターの DPI が変わっても追随しない。
    // そこで表示先ウィンドウの DPI に合わせて、フォントを作り直す。
    // ピクセル数 = 基準サイズ(pt) × ウィンドウの DPI / 72 になるよう、pt を システム DPI との比で補正する
    static int Scale(int px96, int dpi)
    {
        return (int)Math.Round(px96 * dpi / 96.0);
    }

    // 指定 DPI に合わせて、フォント・余白・最小サイズを決める
    void ApplyDpi(int dpi)
    {
        currentDpi = dpi;
        SuspendLayout();
        float size = uiFontPt * dpi / systemDpi;
        // 古い Font は、まだ参照している部分があり得るので Dispose せず GC に任せる
        Font = new Font(uiFontFamily, size);
        log.Font = LogFont(size);

        MinimumSize = new Size(Scale(420, dpi), Scale(300, dpi));
        buttons.Height = Scale(38, dpi);
        buttons.Padding = new Padding(Scale(6, dpi), Scale(4, dpi), Scale(6, dpi), 0);
        // ログ欄の余白は、全角 1 文字(1em)の半分
        int pad = (int)Math.Round(uiFontPt * dpi / 72.0 / 2.0);
        logHost.Padding = new Padding(pad);
        ResumeLayout(true);
    }

    void OnDragEnter(object s, DragEventArgs e)
    {
        e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
    }

    void OnDragDrop(object s, DragEventArgs e)
    {
        var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
        if (paths != null) Enqueue(paths);
    }

    void PickFiles()
    {
        using (var d = new OpenFileDialog { Multiselect = true, Title = "ファイルを選択" })
            if (d.ShowDialog(this) == DialogResult.OK) Enqueue(d.FileNames);
    }

    void PickFolder()
    {
        using (var d = new FolderBrowserDialog())
            if (d.ShowDialog(this) == DialogResult.OK) Enqueue(new[] { d.SelectedPath });
    }

    void Enqueue(string[] paths)
    {
        lock (queue)
        {
            // 中断中に追加されたパスは処理しない
            if (!(running && cancel)) foreach (var p in paths) queue.Enqueue(p);
            if (running) return;
            running = true;
            cancel = false;
            success = failure = skipped = 0;
        }
        ThreadPool.QueueUserWorkItem(delegate { Worker(); });
    }

    void Worker()
    {
        int s, f, k;
        bool cancelled;
        // 前回の出力が残っていれば 1 行あけて区切る
        Log(null, Color.Black);
        while (true)
        {
            string target;
            lock (queue)
            {
                if (queue.Count == 0)
                {
                    running = false;
                    s = success; f = failure; k = skipped;
                    cancelled = cancel;
                    break;
                }
                target = queue.Dequeue();
            }
            // 以降のパス処理は、すべて \\?\ 付きの拡張パスで行う
            string ext = null;
            try { ext = ZoneUtil.ToExtended(target); } catch { }
            if (ext != null && Directory.Exists(ext))
            {
                Log("[FOLDER] " + target + " 内のファイルを処理中...", Color.Black);
                ProcessFolder(ext);
            }
            else
            {
                Log("[FILE] " + target, Color.Black);
                ProcessFile(target);
            }
        }
        Log("", Color.Black);
        Log(string.Format("処理を{0}しました。(成功 {1} / 失敗 {2} / 解除不要 {3})", cancelled ? "中断" : "完了", s, f, k),
            f > 0 ? Color.Red : Color.Black);
    }

    void ProcessFolder(string folder)
    {
        string[] files, dirs;
        try
        {
            files = Directory.GetFiles(folder);
            dirs = Directory.GetDirectories(folder);
        }
        catch (Exception ex)
        {
            Log("  [列挙失敗] " + ZoneUtil.ToDisplay(folder) + " (" + ex.Message + ")", Color.Red);
            Count(ZoneUtil.Result.Failure);
            return;
        }
        foreach (var f in files) { if (cancel) return; ProcessFile(f); }
        foreach (var d in dirs) { if (cancel) return; ProcessFolder(d); }
    }

    void ProcessFile(string path)
    {
        string msg;
        var r = ZoneUtil.Remove(path, out msg);
        Color c = r == ZoneUtil.Result.Success ? Color.Green
                : r == ZoneUtil.Result.Failure ? Color.Red : Color.Gray;
        Log("  " + msg, c);
        Count(r);
    }

    void Count(ZoneUtil.Result r)
    {
        lock (queue)
        {
            if (r == ZoneUtil.Result.Success) success++;
            else if (r == ZoneUtil.Result.Failure) failure++;
            else skipped++;
        }
    }

    // 一覧領域に操作説明を表示する。最初のログ出力で消える
    void ShowHint()
    {
        log.Clear();
        hint.Visible = true;
        showingHint = true;
    }

    // ワーカースレッドからはキューに積むだけにし、画面への反映は UI スレッドのタイマーでまとめて行う
    // (1 行ごとに画面更新を要求すると、大量処理中に UI が詰まり、中断ボタンも効きにくくなる)
    // text が null のときは「前回の出力が残っていれば 1 行あける」区切りを表す
    void Log(string text, Color color)
    {
        lock (pending) pending.Enqueue(new KeyValuePair<string, Color>(text, color));
    }

    void FlushLog()
    {
        bool run;
        lock (queue) run = running && !cancel;
        bCancel.Enabled = run;

        var batch = new List<KeyValuePair<string, Color>>();
        var sw = Stopwatch.StartNew();
        bool appended = false;
        // UI を長時間塞がないよう、1 回の更新は約 50ms で切り上げる
        while (sw.ElapsedMilliseconds < 50)
        {
            lock (pending)
            {
                if (pending.Count == 0) break;
                for (int i = 0; i < 2000 && pending.Count > 0; i++) batch.Add(pending.Dequeue());
            }
            // 同じ色が続く行は 1 回の追記にまとめる(RichTextBox は追記 1 回ごとのコストが大きい)
            var sb = new System.Text.StringBuilder();
            Color cur = Color.Black;
            foreach (var e in batch)
            {
                if (e.Key == null)
                {
                    AppendRun(sb, cur);
                    if (!showingHint && log.TextLength > 0) { log.SelectionStart = log.TextLength; log.AppendText("\n"); }
                    continue;
                }
                if (sb.Length > 0 && e.Value != cur) AppendRun(sb, cur);
                cur = e.Value;
                sb.Append(e.Key).Append('\n');
            }
            AppendRun(sb, cur);
            appended = true;
            batch.Clear();
        }
        if (appended) log.ScrollToCaret();
    }

    void AppendRun(System.Text.StringBuilder sb, Color color)
    {
        if (sb.Length == 0) return;
        if (showingHint) { hint.Visible = false; showingHint = false; }
        log.SelectionStart = log.TextLength;
        log.SelectionFont = log.Font;
        log.SelectionColor = color;
        log.AppendText(sb.ToString());
        sb.Length = 0;
    }

    void Cancel()
    {
        lock (queue)
        {
            if (!running) return;
            cancel = true;
            queue.Clear();
        }
        bCancel.Enabled = false;
    }
}
