@echo off
cd /d D:\YASIN\Backend
dotnet test ".\LUMAR_ERP_API_V2.Tests\LUMAR_ERP_API_V2.Tests.csproj" --filter "ProcessOwnership_ForeignNodeProcess_OnMatchedPort_IsNotKilled|ProcessOwnership_StaleLUMARGatewayProcess_IsStoppedByPreciseIdentity|ProcessOwnership_ProductionCode_DoesNotUseUnsafeNodeKillPatterns" --logger "console;verbosity=minimal"
