@echo off
cd /d D:\YASIN\Backend
dotnet test ".\LUMAR_ERP_API_V2.Tests\LUMAR_ERP_API_V2.Tests.csproj" --filter "LocalWhatsAppFoundationTests" --nologo --logger "console;verbosity=minimal"
