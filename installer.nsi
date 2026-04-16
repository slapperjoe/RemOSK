; RemOSK NSIS Installer Script
; Requires NSIS 3.0 or later

!include "MUI2.nsh"
!include "FileFunc.nsh"

; --------------------------------
; General Configuration
; --------------------------------

!define PRODUCT_NAME "RemOSK"
!define PRODUCT_VERSION "1.0.0"
!define PRODUCT_PUBLISHER "RemOSK Project"
!define PRODUCT_WEB_SITE "https://github.com/yourusername/RemOSK"
!define PRODUCT_UNINST_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_NAME}"
!define PRODUCT_UNINST_ROOT_KEY "HKLM"

Name "${PRODUCT_NAME} ${PRODUCT_VERSION}"
OutFile "RemOSK-Setup.exe"
InstallDir "$PROGRAMFILES64\RemOSK"
InstallDirRegKey HKLM "Software\${PRODUCT_NAME}" "InstallDir"
RequestExecutionLevel admin

; --------------------------------
; Interface Settings
; --------------------------------

!define MUI_ABORTWARNING
!define MUI_ICON "RemOSK\Resources\Icons\app.ico"
!define MUI_UNICON "RemOSK\Resources\Icons\app.ico"
!define MUI_HEADERIMAGE
!define MUI_FINISHPAGE_NOAUTOCLOSE
!define MUI_UNFINISHPAGE_NOAUTOCLOSE

; Finish page options
!define MUI_FINISHPAGE_RUN "$INSTDIR\RemOSK.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Launch RemOSK"
!define MUI_FINISHPAGE_SHOWREADME ""
!define MUI_FINISHPAGE_SHOWREADME_TEXT "Start RemOSK with Windows"
!define MUI_FINISHPAGE_SHOWREADME_FUNCTION AddToStartup

; --------------------------------
; Pages
; --------------------------------

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "README.md"
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH

; --------------------------------
; Languages
; --------------------------------

!insertmacro MUI_LANGUAGE "English"

; --------------------------------
; Installer Sections
; --------------------------------

Section "MainSection" SEC01
  SetOutPath "$INSTDIR"
  SetOverwrite on
  
  ; Copy main executable and dependencies
  File "RemOSK\bin\Release\net10.0-windows\RemOSK.exe"
  File "RemOSK\bin\Release\net10.0-windows\RemOSK.dll"
  File "RemOSK\bin\Release\net10.0-windows\RemOSK.runtimeconfig.json"
  
  ; Copy Assets folder
  SetOutPath "$INSTDIR\Assets"
  File "RemOSK\bin\Release\net10.0-windows\Assets\*.json"
  
  ; Copy Resources folder (if exists in output)
  SetOutPath "$INSTDIR\Resources\Icons"
  File "RemOSK\Resources\Icons\app.ico"
  
  ; Create Start Menu shortcuts
  SetOutPath "$INSTDIR"
  CreateDirectory "$SMPROGRAMS\RemOSK"
  CreateShortcut "$SMPROGRAMS\RemOSK\RemOSK.lnk" "$INSTDIR\RemOSK.exe"
  CreateShortcut "$SMPROGRAMS\RemOSK\Uninstall RemOSK.lnk" "$INSTDIR\uninst.exe"
  
  ; Create Desktop shortcut (optional)
  CreateShortcut "$DESKTOP\RemOSK.lnk" "$INSTDIR\RemOSK.exe"
SectionEnd

Section -AdditionalIcons
  WriteIniStr "$INSTDIR\${PRODUCT_NAME}.url" "InternetShortcut" "URL" "${PRODUCT_WEB_SITE}"
  CreateShortcut "$SMPROGRAMS\RemOSK\Website.lnk" "$INSTDIR\${PRODUCT_NAME}.url"
SectionEnd

Section -Post
  ; Write uninstaller
  WriteUninstaller "$INSTDIR\uninst.exe"
  
  ; Write registry keys for Add/Remove Programs
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayName" "$(^Name)"
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "UninstallString" "$INSTDIR\uninst.exe"
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayIcon" "$INSTDIR\RemOSK.exe"
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayVersion" "${PRODUCT_VERSION}"
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "URLInfoAbout" "${PRODUCT_WEB_SITE}"
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "Publisher" "${PRODUCT_PUBLISHER}"
  
  ; Get installed size
  ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
  IntFmt $0 "0x%08X" $0
  WriteRegDWORD ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "EstimatedSize" "$0"
  
  ; Write install directory to registry
  WriteRegStr HKLM "Software\${PRODUCT_NAME}" "InstallDir" "$INSTDIR"
SectionEnd

; --------------------------------
; Uninstaller Section
; --------------------------------

Section Uninstall
  ; Remove startup entry if exists
  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "RemOSK"
  
  ; Stop RemOSK if running
  nsExec::ExecToStack 'taskkill /F /IM RemOSK.exe /T'
  Pop $0
  
  ; Delete files
  Delete "$INSTDIR\RemOSK.exe"
  Delete "$INSTDIR\RemOSK.dll"
  Delete "$INSTDIR\RemOSK.runtimeconfig.json"
  Delete "$INSTDIR\Assets\*.json"
  Delete "$INSTDIR\Resources\Icons\app.ico"
  Delete "$INSTDIR\uninst.exe"
  Delete "$INSTDIR\${PRODUCT_NAME}.url"
  
  ; Remove directories
  RMDir "$INSTDIR\Assets"
  RMDir "$INSTDIR\Resources\Icons"
  RMDir "$INSTDIR\Resources"
  RMDir "$INSTDIR"
  
  ; Remove shortcuts
  Delete "$SMPROGRAMS\RemOSK\RemOSK.lnk"
  Delete "$SMPROGRAMS\RemOSK\Uninstall RemOSK.lnk"
  Delete "$SMPROGRAMS\RemOSK\Website.lnk"
  RMDir "$SMPROGRAMS\RemOSK"
  Delete "$DESKTOP\RemOSK.lnk"
  
  ; Remove registry keys
  DeleteRegKey ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}"
  DeleteRegKey HKLM "Software\${PRODUCT_NAME}"
  
  SetAutoClose true
SectionEnd

; --------------------------------
; Functions
; --------------------------------

Function AddToStartup
  ; Add RemOSK to Windows startup
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "RemOSK" "$INSTDIR\RemOSK.exe"
FunctionEnd

Function .onInit
  ; Check if already installed
  ReadRegStr $0 ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "UninstallString"
  StrCmp $0 "" done
  
  MessageBox MB_OKCANCEL|MB_ICONEXCLAMATION \
  "${PRODUCT_NAME} is already installed.$\n$\nClick 'OK' to remove the previous version or 'Cancel' to cancel this upgrade." \
  IDOK uninst
  Abort
  
uninst:
  ; Run the uninstaller
  ClearErrors
  ExecWait '$0 _?=$INSTDIR'
  IfErrors no_remove_uninstaller done
  
no_remove_uninstaller:
  MessageBox MB_OK|MB_ICONEXCLAMATION "Uninstallation failed. Please uninstall manually first."
  Abort
  
done:
FunctionEnd

Function un.onInit
  MessageBox MB_ICONQUESTION|MB_YESNO|MB_DEFBUTTON2 "Are you sure you want to uninstall $(^Name)?" IDYES +2
  Abort
FunctionEnd

Function un.onUninstSuccess
  HideWindow
  MessageBox MB_ICONINFORMATION|MB_OK "$(^Name) was successfully removed from your computer."
FunctionEnd
