using System;
using System.IO;
using UnityEngine;

/// <summary>
/// セーブデータの物理I/Oを抽象化するバックエンド。
///
/// 【なぜ抽象化するか】
///   Switch はセーブ領域のマウント→書き込み→明示コミットという手順が必須で、
///   File 直書き（旧実装）のままでは移植できない。プラットフォーム差は
///   このインターフェースの実装差し替えだけで吸収し、SaveManager 側の
///   ロジックには一切持ち込まない方針。
///
/// 【契約】
///   - WriteAllText は「呼び出しが返った時点で読み返せる」こと（Read-your-writes）。
///   - Commit は「耐久ストレージへの確定」。File 実装では書き込み時に完結して
///     いるので no-op。Switch 実装ではジャーナルコミットに相当する処理を行う。
///   - fileName はファイル名のみ（パス区切りを含まない）。置き場所は実装が決める。
/// </summary>
public interface ISaveBackend
{
    bool Exists(string fileName);

    /// <summary>ファイル内容を読む。存在しなければ null。</summary>
    string ReadAllText(string fileName);

    void WriteAllText(string fileName, string contents);

    void Delete(string fileName);

    /// <summary>耐久ストレージへの確定。書き込みバッチの最後に1回呼ぶ。</summary>
    void Commit();
}

/// <summary>
/// バックエンドの選択点。既定は FileSaveBackend（persistentDataPath 直下）。
/// Switch 対応時はブート時にここを差し替える（それ以外のコードは無改修）。
/// </summary>
public static class SaveBackend
{
    private static ISaveBackend instance;

    public static ISaveBackend Instance
    {
        get
        {
            if (instance == null) instance = new FileSaveBackend();
            return instance;
        }
        set { instance = value; }
    }
}

/// <summary>
/// Application.persistentDataPath 直下にファイルとして保存する標準実装。
/// Android / iOS / Windows / macOS / Steam はこれで足りる。
///
/// 【アトミック書き込み】
///   いったん .tmp に全文を書いてから本ファイルへ差し替える。
///   書き込み途中でプロセスが死んでも、本ファイルは「旧内容のまま」か
///   「新内容」のどちらかにしかならず、中途半端な JSON が残らない。
/// </summary>
public class FileSaveBackend : ISaveBackend
{
    private static string PathOf(string fileName)
        => Path.Combine(Application.persistentDataPath, fileName);

    public bool Exists(string fileName) => File.Exists(PathOf(fileName));

    public string ReadAllText(string fileName)
    {
        string path = PathOf(fileName);
        if (!File.Exists(path)) return null;
        return File.ReadAllText(path);
    }

    public void WriteAllText(string fileName, string contents)
    {
        string path = PathOf(fileName);
        string tmp = path + ".tmp";

        File.WriteAllText(tmp, contents);

        if (File.Exists(path))
        {
            try
            {
                // 同一ボリューム内の置き換え（原子的）
                File.Replace(tmp, path, null);
            }
            catch (Exception)
            {
                // File.Replace 非対応環境向けフォールバック
                File.Delete(path);
                File.Move(tmp, path);
            }
        }
        else
        {
            File.Move(tmp, path);
        }
    }

    public void Delete(string fileName)
    {
        string path = PathOf(fileName);
        if (File.Exists(path)) File.Delete(path);
        // 中断死で残った .tmp も掃除する
        string tmp = path + ".tmp";
        if (File.Exists(tmp)) File.Delete(tmp);
    }

    public void Commit()
    {
        // ファイル実装では WriteAllText 時点で完結しているため何もしない。
        // （Switch 実装ではここでセーブジャーナルのコミットを行う）
    }
}
