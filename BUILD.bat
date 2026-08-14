@echo off
chcp 65001 >nul
set SRC=%~dp0
set OUT=E:\GovMiningApp_Publish
dotnet publish "%SRC%GovernmentMiningApp.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=embedded -o "%OUT%"
if errorlevel 1 exit /b 1
echo.
echo EXE: %OUT%\برنامه قانونی و مجوزدار سفارشی دولت.exe
