# 拾日 · 花笺日记与便签

Windows 2.0.0 将原日记入口替换为按日期归档的花笺工作区。使用花笺 MIT 许可的 MarkdownPreview、任务列表、提示块、磁贴与配色组件；日期工作区、本地存储、WebView2桥接、迁移与并发保护为拾日适配实现。没有将 Tauri/Rust 后台、上游自动更新或托盘壳作为另一个程序打包启动。

上游：https://github.com/Achilng/floral-notepaper ，参考提交 23e6df7bfaf785c0bbf7f7c2c05b6d01a53a59e9。完整 MIT 文本在 LICENSE.floral-notepaper；其他依赖许可证在 THIRD_PARTY_NOTICES.md。

功能：一天多篇日记和便签，日期修改，当天/全部/回收站筛选，全文搜索，Markdown 编辑/预览/分栏，GFM表格/任务列表和数学公式，本机图片、图片粘贴/拖入，Markdown导入导出，独立置顶桌面便签、重启恢复已打开便签。便签窗口不恢复上次精确位置/尺寸，使用默认大小。

数据位于程序旁 data/floral-notes.json 和 data/journal-images。旧版 diary.json 迁移前复制为 before-floral-diary.json，原文件及 diary-images 保留。旧图文内容的加粗、斜体、图片转换为 Markdown；导入只执行一次。损坏文件不会覆盖；缺失图片保留引用。

保存失败保留编辑草稿；编辑区保存在本地浏览器草稿存储，以便中断后恢复。多个窗口版本冲突拒绝旧版本覆盖。关闭和完整备份先等待前端保存确认。浏览器档案在 data/.journal-webview2，不进入运行包与完整本机备份。

页面内容禁止外部图片请求；Markdown的原始HTML不执行；外部网页链接只有点击后才通过系统浏览器打开。所有依赖和公式字体随包内置，无需在线CDN。

运行：Windows 10/11 x64，.NET Framework 4.7.2+，Microsoft Edge WebView2 Runtime。官方安装入口：https://developer.microsoft.com/microsoft-edge/webview2/ 。运行包包括SDK桥接库，但不包含完整浏览器运行时。

从统一项目根构建：
1. Windows/release/restore-webview2.ps1（首次联网下载已锁定版本SDK并校验SHA256）
2. npm ci --prefix Windows/desktop/floral --ignore-scripts --no-audit --no-fund
3. npm run build --prefix Windows/desktop/floral
4. Windows/desktop/source/build.ps1

Node 24用于开发，不是用户运行依赖。新版需要完整运行包，不能只替换 Shiri.exe。

本次未给 Android 添加日记；原有手机功能范围保留。
