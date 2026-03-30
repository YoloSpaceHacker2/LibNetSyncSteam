@echo off
set DOTNET=C:\Program Files\dotnet\dotnet.exe

"%DOTNET%" test YSHSteamNetTestApp --logger "console;verbosity=normal"
