@echo off
set DOTNET=C:\Program Files\dotnet\dotnet.exe

echo === RUN ===
"%DOTNET%" run --project YSHSteamNetApp\YSHSteamNetApp.csproj -r win-x64 --steam 
