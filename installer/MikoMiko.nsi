; MikoMiko 0.1.59 安装脚本（NSIS 3.x，Unicode，单用户安装，无需管理员权限）
Unicode true
SetCompressor /SOLID lzma

!include "MUI2.nsh"
!include "FileFunc.nsh"

!define APP_NAME "MikoMiko"
!define APP_VERSION "0.1.59"
!define APP_EXE "MikoMiko.exe"
!define APP_PUBLISHER "MikoMiko Contributors"
!define APP_URL "https://github.com/Huiusto/MikoMiko"
!define ROOT "C:\Users\35847\Doubao\chats\2026-11-22\new-chat\MikoMiko"

Name "${APP_NAME} ${APP_VERSION}"
OutFile "${ROOT}\dist\MikoMiko-Setup-${APP_VERSION}.exe"
InstallDir "$LOCALAPPDATA\Programs\MikoMiko"
InstallDirRegKey HKCU "Software\MikoMiko" "InstallDir"
RequestExecutionLevel user

VIProductVersion "0.1.59.0"
VIAddVersionKey /LANG=2052 "ProductName" "${APP_NAME}"
VIAddVersionKey /LANG=2052 "CompanyName" "${APP_PUBLISHER}"
VIAddVersionKey /LANG=2052 "FileDescription" "${APP_NAME} 安装程序"
VIAddVersionKey /LANG=2052 "FileVersion" "${APP_VERSION}"
VIAddVersionKey /LANG=2052 "ProductVersion" "${APP_VERSION}"
VIAddVersionKey /LANG=2052 "LegalCopyright" "MIT License"

!define MUI_ICON "${ROOT}\src\MikoMiko\Assets\icon.ico"
!define MUI_UNICON "${ROOT}\src\MikoMiko\Assets\icon.ico"
!define MUI_ABORTWARNING

; 萌系蓝白配色：左侧大图、顶部横幅与卸载页均使用同一套银发少女插画。
!define MUI_WELCOMEFINISHPAGE_BITMAP "${ROOT}\installer\assets\install-welcome.bmp"
!define MUI_UNWELCOMEFINISHPAGE_BITMAP "${ROOT}\installer\assets\uninstall-welcome.bmp"
!define MUI_HEADERIMAGE
!define MUI_HEADERIMAGE_BITMAP "${ROOT}\installer\assets\install-header.bmp"
!define MUI_HEADERIMAGE_UNBITMAP "${ROOT}\installer\assets\uninstall-header.bmp"
!define MUI_HEADERIMAGE_RIGHT

!define MUI_WELCOMEPAGE_TITLE "欢迎使用 MikoMiko"
!define MUI_WELCOMEPAGE_TEXT "MikoMiko 会陪你一起逛米游社、追番看剧、记录抽卡，也会安静地待在桌面一角。$\r$\n$\r$\n本次将安装 MikoMiko ${APP_VERSION}，点击「下一步」开始。"
!define MUI_FINISHPAGE_TITLE "安装完成！"
!define MUI_FINISHPAGE_TEXT "MikoMiko ${APP_VERSION} 已经安装好啦，少女正在等你打开。$\r$\n$\r$\n以后可以在开始菜单或桌面找到它。"
!define MUI_FINISHPAGE_RUN "$INSTDIR\${APP_EXE}"
!define MUI_FINISHPAGE_RUN_TEXT "立即运行 MikoMiko"

!define MUI_UNWELCOMEPAGE_TITLE "卸载 MikoMiko"
!define MUI_UNWELCOMEPAGE_TEXT "要送少女离开了吗？卸载后会删除程序文件与开始菜单快捷方式，账号与本地记录会保留。$\r$\n$\r$\n点击「卸载」继续。"
!define MUI_UNFINISHPAGE_TITLE "卸载完成"
!define MUI_UNFINISHPAGE_TEXT "MikoMiko 已从这台电脑移除。$\r$\n$\r$\n谢谢你曾经让它陪你一段时间。"

!insertmacro MUI_PAGE_WELCOME
; 允许用户自选安装位置；升级时目录页会默认带出注册表里上一次的 InstallDir。
!define MUI_DIRECTORYPAGE_TEXT_TOP "选择 MikoMiko 的安装位置。升级安装时会默认沿用上一次的目录，也可以改到其他磁盘。"
!define MUI_DIRECTORYPAGE_TEXT_DESTINATION "安装到："
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_WELCOME
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH

!insertmacro MUI_LANGUAGE "SimpChinese"
!insertmacro MUI_LANGUAGE "English"
!insertmacro MUI_LANGUAGE "TradChinese"

Section "MikoMiko 主程序" SecMain
  SectionIn RO
  SetOutPath "$INSTDIR"
  SetOverwrite on

  ; 覆盖安装前结束正在运行的旧版本，避免主程序被占用导致更新失败。
  nsExec::Exec 'taskkill /IM "${APP_EXE}" /F'
  Sleep 1200
  Delete "$INSTDIR\${APP_EXE}"
  File "C:\Users\35847\Doubao\chats\2026-11-22\new-chat\MikoMiko\dist\app\MikoMiko.exe"
  SetOutPath "$INSTDIR\Assets\Anime"
  File "/nonfatal" "C:\Users\35847\Doubao\chats\2026-11-22\new-chat\MikoMiko\dist\app\Assets\Anime\anime-module.zip"
  SetOutPath "$INSTDIR\Assets\Anime\flutter"
  File /r "C:\Users\35847\Doubao\chats\2026-11-22\new-chat\MikoMiko\dist\app\Assets\Anime\flutter\*"
  SetOutPath "$INSTDIR"
  File /nonfatal "C:\Users\35847\Doubao\chats\2026-11-22\new-chat\MikoMiko\README.md"
  File /nonfatal "C:\Users\35847\Doubao\chats\2026-11-22\new-chat\MikoMiko\LICENSE"

  WriteUninstaller "$INSTDIR\Uninstall.exe"

  CreateDirectory "$SMPROGRAMS\MikoMiko"
  CreateShortcut "$SMPROGRAMS\MikoMiko\MikoMiko.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0
  CreateShortcut "$SMPROGRAMS\MikoMiko\卸载 MikoMiko.lnk" "$INSTDIR\Uninstall.exe"
  CreateShortcut "$DESKTOP\MikoMiko.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0

  WriteRegStr HKCU "Software\MikoMiko" "InstallDir" "$INSTDIR"
  WriteRegStr HKCU "Software\MikoMiko" "Version" "${APP_VERSION}"

  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\MikoMiko" "DisplayName" "${APP_NAME}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\MikoMiko" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\MikoMiko" "Publisher" "${APP_PUBLISHER}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\MikoMiko" "DisplayIcon" "$INSTDIR\${APP_EXE}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\MikoMiko" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\MikoMiko" "URLInfoAbout" "${APP_URL}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\MikoMiko" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\MikoMiko" "NoModify" 1
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\MikoMiko" "NoRepair" 1

  ; 安装阶段就把启动页壁纸和页面资源放到用户目录，并预热官方壁纸缓存。
  ExecWait '"$INSTDIR\${APP_EXE}" --prepare-launch'

  ; 应用内更新使用 /S 静默安装：没有完成页，安装后直接拉起新版本。
  IfSilent 0 +1
  Exec '"$INSTDIR\${APP_EXE}"'
SectionEnd

Section "Uninstall"
  ; 软件内点「卸载」会先退出主程序；这里再兜底结束残留进程，确保程序文件不被占用。
  nsExec::Exec 'taskkill /IM "${APP_EXE}" /F'
  Sleep 1200
  Delete "$DESKTOP\MikoMiko.lnk"
  RMDir /r "$SMPROGRAMS\MikoMiko"

  Delete "$INSTDIR\Uninstall.exe"
  Delete "$INSTDIR\${APP_EXE}"
  Delete "$INSTDIR\README.md"
  Delete "$INSTDIR\LICENSE"
  RMDir /r "$INSTDIR\Assets"
  RMDir "$INSTDIR"

  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\MikoMiko"
  DeleteRegKey HKCU "Software\MikoMiko"
SectionEnd






