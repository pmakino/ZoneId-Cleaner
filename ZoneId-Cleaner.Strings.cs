using System;
using System.Collections.Generic;
using System.Globalization;

// 画面に表示する文言の言語別テーブル (国際化)
// - 表示言語は Windows の表示言語に自動で従う。対応していない言語は英語で表示する
// - 言語を追加するときは、辞書を 1 つ足し、Select() に 1 行足す。
//   キーは英語の辞書 (En) を基準にし、足りないキーは英語で表示する
static class Res
{
    static readonly Dictionary<string, string> En = new Dictionary<string, string>
    {
        { "title",          "ZoneId Cleaner" },
        { "btn_file",       "Select files..." },
        { "btn_folder",     "Select folders..." },
        { "btn_clear",      "Clear log" },
        { "btn_cancel",     "Cancel" },
        { "dlg_files",      "Select files" },
        { "hint",           "Drop files or folders here to remove their Zone.Identifier.\n(Multiple items allowed.)" },

        // ログ欄の等幅フォント。日本語は日本語の字形のあるフォントにする
        { "log_font",       "Consolas" },

        { "log_folder",     "[FOLDER] {0} : processing files..." },
        { "log_file",       "[FILE] {0}" },
        { "log_removed",    "[Removed] {0}" },
        { "log_not_needed", "[Not needed] {0}" },
        { "log_failed",     "[Failed] {0} ({1})" },
        { "log_failed_code","[Failed] {0} (error {1}: {2})" },
        { "log_enum_failed","[Cannot list] {0} ({1})" },

        // 完了・中断のメッセージ。{0} 成功 / {1} 失敗 / {2} 解除不要
        { "done",           "Done. (Removed {0} / Failed {1} / Not needed {2})" },
        { "cancelled",      "Cancelled. (Removed {0} / Failed {1} / Not needed {2})" },
    };

    static readonly Dictionary<string, string> Ja = new Dictionary<string, string>
    {
        { "title",          "ZoneId 一括解除ツール" },
        { "btn_file",       "ファイルを選択..." },
        { "btn_folder",     "フォルダーを選択..." },
        { "btn_clear",      "ログをクリア" },
        { "btn_cancel",     "中断" },
        { "dlg_files",      "ファイルを選択" },
        { "hint",           "Zone.Identifier を解除したいファイルまたはフォルダーを、\nこのウィンドウにドロップしてください(複数可)。" },

        // Consolas には日本語の字形がなく、日本語を含む行だけ別のフォントになるため、MS ゴシックにする
        { "log_font",       "MS Gothic" },

        { "log_folder",     "[FOLDER] {0} 内のファイルを処理中..." },
        { "log_file",       "[FILE] {0}" },
        { "log_removed",    "[解除成功] {0}" },
        { "log_not_needed", "[解除不要] {0}" },
        { "log_failed",     "[解除失敗] {0} ({1})" },
        { "log_failed_code","[解除失敗] {0} (エラー {1}: {2})" },
        { "log_enum_failed","[列挙失敗] {0} ({1})" },

        { "done",           "処理を完了しました。(成功 {0} / 失敗 {1} / 解除不要 {2})" },
        { "cancelled",      "処理を中断しました。(成功 {0} / 失敗 {1} / 解除不要 {2})" },
    };

    static readonly Dictionary<string, string> current = Select();

    // Windows の表示言語から、使う辞書を選ぶ
    static Dictionary<string, string> Select()
    {
#if FORCE_EN
        // 動作確認用 (通常のビルドには含まれない): 英語に固定する
        return En;
#else
        switch (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName)
        {
            case "ja": return Ja;
            default: return En;
        }
#endif
    }

    // 文言を返す。現在の言語になければ英語、英語にもなければキー名を返す
    public static string T(string key)
    {
        string s;
        if (current.TryGetValue(key, out s)) return s;
        if (En.TryGetValue(key, out s)) return s;
        return key;
    }

    // 書式付きの文言を返す ({0} などを args で置き換える)
    public static string F(string key, params object[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, T(key), args);
    }
}
