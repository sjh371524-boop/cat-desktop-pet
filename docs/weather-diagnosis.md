# 天气加载失败：复现与修复

2026-09-03 在桌宠同样的 .NET Framework 编译环境中运行 `scripts/WeatherNetworkProbe.cs`，结果：

1. 默认协议 `Ssl3, Tls`：请求 `api.open-meteo.com` 报“未能创建 SSL/TLS 安全通道”。
2. 同一经纬度、同一 URL，启用 TLS 1.2：HTTP 200，当前天气代码 3（阴）。
3. 可选地理编码地址同样在 TLS 1.2 下 HTTP 200；国家地区本身并非问题所在。

沙盒中的第一次探测是套接字权限拒绝，不能据此判断用户网络。以上因果对比使用桌宠实际网络权限重新执行，区分了沙盒限制与程序握手错误。

旧逻辑还存在两个放大问题：每次先串行等待无必要的地理编码请求；所有网络/解析错误被吞掉，缓存为 null 时永久显示“天气加载中”。新实现保留正确的位置配置，直接请求天气，启用 TLS 1.2，不绕过证书验证。启动预加载、持久缓存、合并并发请求、重试退避与明确失败状态共同避免再次无限等待。不会把空 weather_code 当晴天。

真实联网复核使用 `dist/CodexCat.exe --verify-weather-live`；输出记录在 `tests/weather-live.txt`。网络无关回归使用 `--verify-weather`，覆盖代码映射、无效响应、并发合并、失败状态、退避、缓存恢复、换地点与过期处理。

气泡图标完全在 WPF 中绘制并动画化。预览测试分别对所有 8 种状态采样两次，验证画面实际变化，并验证隐藏气泡时动画时钟停止。

参考：[微软 .NET Framework TLS 文档](https://learn.microsoft.com/en-us/dotnet/framework/network-programming/tls)、[Open-Meteo 接口与天气代码](https://open-meteo.com/en/docs)。
