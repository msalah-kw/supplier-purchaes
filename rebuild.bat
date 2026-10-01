@echo off
cd /d "%~dp0"
echo ============================================
echo  Close the app first if it is open.
echo  Rebuilding... please wait 1-2 minutes
echo ============================================
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:EnableCompressionInSingleFile=true -o .\publish
if errorlevel 1 (
  echo.
  echo *** BUILD FAILED ***
  echo Make sure the app is closed and .NET SDK 8 is installed, then try again.
) else (
  echo.
  echo DONE! Run the app from: publish\SupplierPurchases.exe
)
echo.
pause
