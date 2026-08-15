using System;
using System.Collections.Generic;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using LYBox.Plugin.BTSou.Models;
using MySql.Data.MySqlClient;

namespace LYBox.Plugin.BTSou.Services;

/// <summary>
/// BTSOU MySQL 数据库服务。
/// 复刻自原程序：授权锁（lockchange 表）与举报（reportdata 表）。
/// 原逻辑直接字符串拼接 SQL（存在注入风险），此处保留原始行为但暴露参数化查询接口。
/// </summary>
public class BTSouDatabaseService
{
    /// <summary>静态单例（ViewModel 由生成器无参构造，服务经单例访问）</summary>
    public static BTSouDatabaseService Current { get; } = new();

    private readonly string _connectionString;

    public BTSouDatabaseService()
    {
        _connectionString = BTSouConfig.MySqlConnectionString;
    }

    /// <summary>
    /// DES 解密（复刻原程序类 c::a()，用于解密加密的配置串）。
    /// </summary>
    public static string DesDecrypt(string base64Text)
    {
        try
        {
            byte[] key = Encoding.UTF8.GetBytes(BTSouConfig.DesKey);
            byte[] iv = BTSouConfig.DesIv;
            byte[] data = Convert.FromBase64String(base64Text);
            using var des = System.Security.Cryptography.DES.Create();
            using var ms = new MemoryStream();
            using (var cs = new CryptoStream(ms, des.CreateDecryptor(key, iv), CryptoStreamMode.Write))
            {
                cs.Write(data, 0, data.Length);
                cs.FlushFinalBlock();
            }
            return Encoding.UTF8.GetString(ms.ToArray());
        }
        catch
        {
            return base64Text;
        }
    }

    /// <summary>
    /// 获取本机硬盘序列号（原程序用 wmic cpu get processorid + 卷序列号拼接）。
    /// </summary>
    public static string GetHardDiskSerial()
    {
        try
        {
            string cpu = "";
            string volume = "";
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c wmic cpu get processorid")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                using var p = System.Diagnostics.Process.Start(psi)!;
                cpu = p.StandardOutput.ReadToEnd().Trim();
            }
            catch { }

            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c vol C:")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                using var p = System.Diagnostics.Process.Start(psi)!;
                volume = p.StandardOutput.ReadToEnd().Trim();
            }
            catch { }

            var raw = cpu + volume;
            return string.IsNullOrWhiteSpace(raw) ? "UNKNOWN" : raw;
        }
        catch
        {
            return "UNKNOWN";
        }
    }

    /// <summary>
    /// 查询授权锁状态。
    /// 原逻辑：SELECT 上锁码 FROM lockchange WHERE 硬盘序列号='...'
    /// </summary>
    public async Task<LockInfo> GetLockInfoAsync(string hardDiskSerial, CancellationToken ct = default)
    {
        var info = new LockInfo { HardDiskSerial = hardDiskSerial };
        using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        // 原程序直接拼接，此处改为参数化（保持行为等价）
        const string sql = "SELECT `上锁码` FROM `lockchange` WHERE `硬盘序列号`=@serial LIMIT 1";
        using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@serial", hardDiskSerial);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
            info.LockCode = Convert.ToInt16(reader[0]);
        return info;
    }

    /// <summary>
    /// 若本机未授权，生成 5-8 随机上锁码并写入（原逻辑：lockchange INSERT）。
    /// </summary>
    public async Task<LockInfo> EnsureLicensedAsync(string hardDiskSerial, CancellationToken ct = default)
    {
        var info = await GetLockInfoAsync(hardDiskSerial, ct);
        if (info.LockCode != 0)
            return info;

        info.LockCode = Random.Shared.Next(5, 9);
        info.UpdateTime = DateTime.Now;

        const string sql = "INSERT INTO `lockchange` (`硬盘序列号`, `上锁码`, `更新日期`) VALUES (@serial, @code, @time)";
        using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@serial", hardDiskSerial);
        cmd.Parameters.AddWithValue("@code", info.LockCode);
        cmd.Parameters.AddWithValue("@time", DateTime.Now.ToString());
        await cmd.ExecuteNonQueryAsync(ct);
        return info;
    }

    /// <summary>
    /// 提交举报（原逻辑：reportdata INSERT）。
    /// </summary>
    public async Task<bool> SubmitReportAsync(ReportEntry entry, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO `reportdata`
                (`硬盘序列号`, `标题`, `磁链`, `提交时间`, `姓名`, `邮箱`, `手机号`, `身份证号`, `危害类别`, `详细描述`)
            VALUES
                (@serial, @title, @magnet, @time, @name, @email, @phone, @idcard, @category, @desc)
            """;
        using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@serial", entry.HardDiskSerial);
        cmd.Parameters.AddWithValue("@title", entry.Title);
        cmd.Parameters.AddWithValue("@magnet", entry.MagnetLink);
        cmd.Parameters.AddWithValue("@time", DateTime.Now.ToString());
        cmd.Parameters.AddWithValue("@name", entry.Name);
        cmd.Parameters.AddWithValue("@email", entry.Email);
        cmd.Parameters.AddWithValue("@phone", entry.Phone);
        cmd.Parameters.AddWithValue("@idcard", entry.IdCard);
        cmd.Parameters.AddWithValue("@category", entry.Category);
        cmd.Parameters.AddWithValue("@desc", entry.Description);
        await cmd.ExecuteNonQueryAsync(ct);
        return true;
    }

    /// <summary>
    /// 按危害类别查询磁链列表（原逻辑：SELECT 磁链 FROM reportdata WHERE 危害类别='...'）。
    /// </summary>
    public async Task<List<string>> GetMagnetsByCategoryAsync(string category, CancellationToken ct = default)
    {
        var list = new List<string>();
        const string sql = "SELECT `磁链` FROM `reportdata` WHERE `危害类别`=@category";
        using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@category", category);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(reader[0].ToString() ?? "");
        return list;
    }

    /// <summary>
    /// 加载全部举报数据到 DataTable（原逻辑：SELECT * FROM reportdata）。
    /// </summary>
    public async Task<DataTable> LoadReportDataAsync(CancellationToken ct = default)
    {
        var table = new DataTable();
        using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        using var cmd = new MySqlCommand("SELECT * FROM `reportdata`", conn);
        using var adapter = new MySqlDataAdapter(cmd);
        await Task.Run(() => adapter.Fill(table), ct);
        return table;
    }
}
