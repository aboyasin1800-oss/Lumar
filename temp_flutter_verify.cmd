@echo off
cd /d D:\YASIN\frontend\tailoring_system
D:\YASIN\Tools\FlutterSDK\bin\flutter.bat pub get
D:\YASIN\Tools\FlutterSDK\bin\flutter.bat pub deps
D:\YASIN\Tools\FlutterSDK\bin\flutter.bat analyze
D:\YASIN\Tools\FlutterSDK\bin\flutter.bat test
D:\YASIN\Tools\FlutterSDK\bin\flutter.bat build windows
