@echo off
chcp 65001 >nul
cd /d "%~dp0"

set "PY=C:\Users\asus\.dsh\dsh-runtimes\dsh-primary-runtime\dependencies\python\python.exe"
if not exist "%PY%" set "PY=python"
set "BASH=C:\Program Files\Git\bin\bash.exe"
if not exist "%BASH%" set "BASH=bash"

echo.
echo   [1/4] 重新生成数据
"%PY%" tools\build_data.py
if errorlevel 1 goto fail

echo.
echo   [2/4] 复制给三个端
copy /y data\holidays.json desktop\holidays.json >nul
copy /y data\holidays.json app\assets\holidays.json >nul
echo       ok

echo.
echo   [3/4] 发布到 GitHub（三端启动时会自动拉）
"%PY%" tools\publish.py
if errorlevel 1 goto fail

echo.
echo   [4/4] 重新打包
echo   --- Windows exe ---
call desktop\build.bat
echo   --- Android apk ---
"%BASH%" app\build.sh

echo.
echo   完成。
echo   exe : desktop\build\JiaQi.exe
echo   apk : app\build\jiaqi.apk
echo.
echo   注意：GitHub 的 raw 有几分钟 CDN 缓存，刚发布的三端未必立刻拉到，
echo   重开一次就会拿到。
echo.
pause
exit /b 0

:fail
echo.
echo   出错了，往上翻看是第几步失败的。
pause
exit /b 1
