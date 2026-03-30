@echo off
set DOTNET=C:\Program Files\dotnet\dotnet.exe

"%DOTNET%"  new sln -n      SteamNetSolution
"%DOTNET%"  new classlib -n YSHSteamNet
"%DOTNET%"  new console -n  YSHSteamNetTestApp

"%DOTNET%" sln add YSHSteamNet
"%DOTNET%" sln add YSHSteamNetTestApp

cd YSHSteamNetTestApp
"%DOTNET%" add reference ../YSHSteamNet