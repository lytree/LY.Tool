using System;

namespace LYBox.Plugin.BTSou.Models;

/// <summary>
/// BTSOU 精简版配置（仅保留搜索 + 迅雷下载所需）。
/// 已移除全部数据库配置（MySQL 连接串、授权锁表、举报表）。
/// </summary>
public static class BTSouConfig
{
    // 资源池配置地址
    public const string ResPoolUrl = "http://www.pc936.com/u/UpData/BTSou/ResPool.txt";
    public const string UpdateUrl = "http://www.pc936.com/u/UpData/BTSou/UpData.txt";

    // 种子文件镜像
    public const string TorrentMirrorBase = "http://bt.box.n0808.com/54/D0/";

    // 迅雷下载组件（本地 DLL，缺时自动补下）
    public const string ThunderAgentDll = "Interop.ThunderAgentLib.dll";
    public const string ThunderAgentDllUrl = "http://www.pc936.com/u/UpData/MissingFile/Interop.ThunderAgentLib.dll";
}
