using System;
using System.Collections.Generic;
using System.Text;

namespace LYBox.Plugin.BTSou.Models;

/// <summary>
/// BTSOU 数据库配置（源自反编译提取，密钥/IV 与原程序一致）。
/// </summary>
public static class BTSouConfig
{
    // MySQL 连接串（DES 解密后的明文，源自 BTSOU v24.10.24）
    public const string MySqlConnectionString =
        "server=www.pc936.com;port=3306;database=ibtsou;username=ibtsou9394;password=fdJZh3nK2sE5n8Mn;";

    // DES 解密参数（类 c.a 方法）：密钥 xEWjteZ7, IV 固定字节
    public const string DesKey = "xEWjteZ7";
    public static readonly byte[] DesIv = [33, 52, 101, 120, 9, 186, 205, 254];

    // 资源池配置地址
    public const string ResPoolUrl = "http://www.pc936.com/u/UpData/BTSou/ResPool.txt";
    public const string UpdateUrl = "http://www.pc936.com/u/UpData/BTSou/UpData.txt";

    // 种子文件镜像
    public const string TorrentMirrorBase = "http://bt.box.n0808.com/54/D0/";

    // 授权锁表
    public const string TableLockChange = "lockchange";
    // 举报表
    public const string TableReportData = "reportdata";
}
