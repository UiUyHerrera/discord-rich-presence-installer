@echo off
setlocal
set "MSBUILD=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"
if not exist "%MSBUILD%" (
  echo No se encontro MSBuild de .NET Framework.
  exit /b 1
)
"%MSBUILD%" DiscordRichPresenceInstaller.csproj /t:Build /p:Configuration=Release /v:minimal
if errorlevel 1 exit /b %errorlevel%
echo Instalador compilado en bin\DiscordRichPresenceInstaller.exe
