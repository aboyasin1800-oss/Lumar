@echo off
for /f "skip=1" %%p in ('wmic process where "name='node.exe' and CommandLine like '%%LocalWhatsApp%%'" get ProcessId 2^>nul') do taskkill /PID %%p /F
cd /d "d:\YASIN\Gateway\LocalWhatsApp"
node .\scripts\upstream-isolation-probe.mjs 967775106545
