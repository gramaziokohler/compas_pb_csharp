@echo off
REM Build script for CompasPb project
REM Usage: build.bat [Configuration] [--clean] [--skip-tests] [--no-pack]
REM   Configuration: Release (default) or Debug
REM   --clean: Clean before building
REM   --skip-tests: Skip running tests
REM   --no-pack: Skip creating NuGet package

setlocal

REM Parse arguments - defaults
set CONFIG=Release
set CLEAN=false
set SKIP_TESTS=false
set NO_PACK=false

REM Parse all arguments
:parse_args
if "%~1"=="" goto :done_parsing
if /i "%~1"=="Debug" set CONFIG=Debug& shift & goto :parse_args
if /i "%~1"=="Release" set CONFIG=Release& shift & goto :parse_args
if /i "%~1"=="--clean" set CLEAN=true& shift & goto :parse_args
if /i "%~1"=="--skip-tests" set SKIP_TESTS=true& shift & goto :parse_args
if /i "%~1"=="--no-pack" set NO_PACK=true& shift & goto :parse_args
shift
goto :parse_args
:done_parsing


echo ===============================
echo CompasPb Build Script
echo Configuration: %CONFIG%
echo ===============================
echo.

REM Clean if requested
if "%CLEAN%"=="true" (
    echo [0/4] Cleaning...
    dotnet clean
    for /d /r %%d in (bin,obj) do @if exist "%%d" rd /s /q "%%d"
    echo.
)

REM Restore dependencies
echo [1/4] Restoring dependencies...
dotnet restore
if %ERRORLEVEL% neq 0 (
    echo ERROR: Restore failed
    exit /b %ERRORLEVEL%
)
echo.

REM Build
echo [2/4] Building solution (%CONFIG%)...
dotnet build --configuration %CONFIG% --no-restore
if %ERRORLEVEL% neq 0 (
    echo ERROR: Build failed
    exit /b %ERRORLEVEL%
)
echo.

REM Run tests
if "%SKIP_TESTS%"=="false" (
    echo [3/4] Running tests...
    dotnet test --configuration %CONFIG% --no-build --verbosity normal
    if %ERRORLEVEL% neq 0 (
        echo ERROR: Tests failed
        exit /b %ERRORLEVEL%
    )
    echo.
) else (
    echo [3/4] Skipping tests
    echo.
)

REM Create NuGet package
if "%NO_PACK%"=="false" (
    echo [4/4] Creating NuGet package...
    dotnet pack src\CompasPb\CompasPb.csproj --configuration %CONFIG% --no-build --output .\artifacts
    if %ERRORLEVEL% neq 0 (
        echo ERROR: Pack failed
        exit /b %ERRORLEVEL%
    )
    echo.
    echo ===============================
    echo Build completed successfully!
    echo Package location: .\artifacts
    echo ===============================
) else (
    echo [4/4] Skipping package creation
    echo.
    echo ===============================
    echo Build completed successfully!
    echo ===============================
)

endlocal
