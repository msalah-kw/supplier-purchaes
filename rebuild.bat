@echo off
cd /d "%~dp0"
echo ============================================
echo  Close the app first if it is open.
echo  Rebuilding... please wait 1-2 minutes
echo ============================================
dotnet publish -c Release -o "%TEMP%\SupplierPurchasesPublish"
if errorlevel 1 (
  echo.
  echo *** BUILD FAILED ***
  echo Make sure the app is closed and .NET SDK 8 is installed, then try again.
  goto end
)
copy /y "%TEMP%\SupplierPurchasesPublish\SupplierPurchases.exe" "SupplierPurchases.exe" >nul
rmdir /s /q "%TEMP%\SupplierPurchasesPublish"
rmdir /s /q bin obj 2>nul
echo.
echo DONE! Run the app from: SupplierPurchases.exe
:end
echo.
pause
