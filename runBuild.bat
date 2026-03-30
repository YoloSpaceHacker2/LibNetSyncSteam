@echo off
set DOTNET=C:\Program Files\dotnet\dotnet.exe

echo === BUILD ===
"%DOTNET%" build YSHSteamNetApp\YSHSteamNetApp.csproj -r win-x64
