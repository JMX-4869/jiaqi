@echo off
chcp 65001 >nul
cd /d "%~dp0"

set "PY=C:\Users\asus\.dsh\dsh-runtimes\dsh-primary-runtime\dependencies\python\python.exe"
if not exist "%PY%" set "PY=python"

echo.
echo   假期表 · 网页版
echo   地址 http://127.0.0.1:8899/web/index.html
echo   关掉这个窗口就停止
echo.

start "" "http://127.0.0.1:8899/web/index.html"
"%PY%" -m http.server 8899 --bind 127.0.0.1
