# 拾日 Windows

拾日是本地优先的日程与学习工具。当前源码版本 **2.0.0**，支持今日任务、长期计划、倒计时、课程表、本机课表图片识别、专注与统计，以及按日期整理的日记和便签。

Windows日记工作区复用[花笺](https://github.com/Achilng/floral-notepaper)的Markdown预览、任务列表和磁贴组件，增加拾日的日期归档与本机存储。可编辑/预览/分栏，使用表格、公式和图片；桌面便签可置顶并在重启后恢复。旧日记文字、样式和图片会在备份后迁移。

- 本仓库：Windows源码，MIT许可范围见[LICENSE_SCOPE.md](LICENSE_SCOPE.md)。
- [拾日网页版](https://wangcary1214-dotcom.github.io/shiri-web/) / [网页版仓库](https://github.com/wangcary1214-dotcom/shiri-web)：独立版本；本仓库不会自动改动线上网站。
- Windows运行要求：Windows10/11 x64、.NET Framework4.7.2+、[Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/)。Node只用于构建。

## 构建

安装Node24，然后在Windows PowerShell或PowerShell7中执行：

```powershell
.\Windows\release\restore-webview2.ps1
.\Windows\release\restore-public-artwork.ps1
npm ci --prefix Windows/desktop/floral --ignore-scripts --no-audit --no-fund
npm run build --prefix Windows/desktop/floral
.\Windows\desktop\source\build.ps1
```

输出为Windows/desktop/Shiri.exe，运行时还须保留同目录WebView2 DLL、floral/dist和OCR脚本。GitHub Actions自动构建并生成可下载的未签名完整基础运行包；以Actions构建结果为准。

本仓库不重新上传本地宠物图片或OCR二进制。构建脚本从已有公开网页版的固定提交获取71张宠物帧并核对SHA256；素材保留其原授权范围，不因为程序采用MIT而变成MIT素材。应用图标文件没有包含在这次源码发布中；源码构建使用默认图标。OCR优先使用单独配置的PaddleOCR-json，基础构建未附带该引擎时使用Windows系统OCR，需已安装可用识别语言。

## 数据与升级

用户数据保存在程序旁data目录。升级前备份整个data；2.0.0须完整包升级，不能只替换EXE。旧日记迁移前保存before-floral-diary.json，原文件与旧图片保留。新日记为floral-notes.json/journal-images，多窗口冲突不会静默覆盖，保存失败保留草稿并阻止关闭。

源码和CI运行包不含用户data、浏览器档案或密钥。[隐私说明](PRIVACY.md)。

## Code signing policy

**尚未取得SignPath审批，当前构建均未签名。** [代码签名政策](CODE_SIGNING_POLICY.md)说明维护者、签名范围和流程；已准备GitHub托管构建与按需SignPath签名工作流，正式启用必须完成维护者申请、角色和密钥配置。签名失败不会被标成已签名。

## 许可与上游

拾日自有Windows代码采用MIT，版权归wangcary1214-dotcom。花笺组件保留Copyright (c) 2026 Achilng及MIT许可。[第三方与素材范围](LICENSE_SCOPE.md)；花笺及前端依赖清单见Windows/desktop/floral/THIRD_PARTY_NOTICES.md。Microsoft WebView2及可选OCR引擎遵循各自许可；不使用花笺的发布者签名。
