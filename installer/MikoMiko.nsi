; MikoMiko 0.1.33 安装脚本（NSIS 3.x，Unicode，单用户安装，无需管理员权限）
Unicode true
SetCompressor /SOLID lzma

!include "MUI2.nsh"
!include "FileFunc.nsh"

!define APP_NAME "MikoMiko"
!define APP_VERSION "0.1.33"
!define APP_EXE "MikoMiko.exe"
!define APP_PUBLISHER "MikoMiko Contributors"
!define APP_URL "https://github.com/Huiusto/MikoMiko"

Name "${APP_NAME} ${APP_VERSION}"
OutFile "C:\Users\35847\Doubao\chats\2026-11-22\new-chat\MikoMiko\dist\MikoMiko-Setup-${APP_VERSION}.exe"
InstallDir "$LOCALAPPDATA\Programs\MikoMiko"
InstallDirRegKey HKCU "Software\MikoMiko" "InstallDir"
RequestExecutionLevel user

VIProductVersion "0.1.33.0"
VIAddVersionKey /LANG=2052 "ProductName" "${APP_NAME}"
VIAddVersionKey /LANG=2052 "CompanyName" "${APP_PUBLISHER}"
VIAddVersionKey /LANG=2052 "FileDescription" "${APP_NAME} 安装程序"
VIAddVersionKey /LANG=2052 "FileVersion" "${APP_VERSION}"
VIAddVersionKey /LANG=2052 "ProductVersion" "${APP_VERSION}"
VIAddVersionKey /LANG=2052 "LegalCopyright" "MIT License"

!define MUI_ICON "C:\Users\35847\Doubao\chats\2026-11-22\new-chat\MikoMiko\src\MikoMiko\Assets\icon.ico"
!define MUI_UNICON "C:\Users\35847\Doubao\chats\2026-11-22\new-chat\MikoMiko\src\MikoMiko\Assets\icon.ico"
!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_RUN "$INSTDIR\${APP_EXE}"
!define MUI_FINISHPAGE_RUN_TEXT "立即运行 MikoMiko"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "SimpChinese"
!insertmacro MUI_LANGUAGE "English"
!insertmacro MUI_LANGUAGE "TradChinese"

Section "MikoMiko 主程序" SecMain
  SectionIn RO
  SetOutPath "$INSTDIR"
  SetOverwrite on

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
SectionEnd

Section "Uninstall"
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


