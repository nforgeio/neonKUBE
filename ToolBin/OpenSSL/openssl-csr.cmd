@echo off
REM Uses OpenSSL to generate a private key and as well as a certificate signing
REM request (CSR) that can be used to request a SSL certificate from a certificate
REM authority.
REM
REM	Usage: openssl-csr <domain>

if not [%1]==[] goto haveHost

echo.
echo Usage: openssl-csr ^<domain^>
echo.
echo        where ^<domain^> 	- domain name of host (FQDN)
echo.

goto exit

:haveHost

set FOLDER=%NR_TEMP%\OpenSSL
if not exist "%FOLDER%" mkdir "%FOLDER%"
rm "%FOLDER%\OpenSSL\*.*"

echo.

openssl req -new -nodes -keyout "%FOLDER%\%1.key" -out "%FOLDER%\%1.csr" -newkey rsa:2048

if %ERRORLEVEL% GTR 0 goto exit

echo *** Operation completed ***
echo.
echo The KEY and CSR file have been written to: %FOLDER%
echo.

:exit