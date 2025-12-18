@echo off
REM Setup script for Azure Speech-to-Text Python application (Windows)
REM This script automates the initial setup process

setlocal EnableDelayedExpansion

echo ==========================================
echo Azure Speech-to-Text Setup Script
echo ==========================================
echo.

REM Check Python version
echo Checking Python version...
python --version >nul 2>&1
if errorlevel 1 (
    echo ERROR: Python is not installed
    echo Please install Python 3.9 or higher from https://www.python.org
    pause
    exit /b 1
)

for /f "tokens=2" %%i in ('python --version 2^>^&1') do set PYTHON_VERSION=%%i
echo Found Python %PYTHON_VERSION%
echo.

REM Create virtual environment
echo Creating virtual environment...
if exist venv (
    echo WARNING: Virtual environment already exists
    set /p recreate="Do you want to recreate it? (y/n): "
    if /i "!recreate!"=="y" (
        rmdir /s /q venv
        python -m venv venv
        echo Virtual environment recreated
    ) else (
        echo Using existing virtual environment
    )
) else (
    python -m venv venv
    echo Virtual environment created
)
echo.

REM Activate virtual environment
echo Activating virtual environment...
call venv\Scripts\activate.bat
echo Virtual environment activated
echo.

REM Upgrade pip
echo Upgrading pip...
python -m pip install --upgrade pip >nul 2>&1
echo pip upgraded
echo.

REM Install dependencies
echo Installing dependencies...
echo This may take a few minutes...
pip install -r requirements.txt >nul 2>&1
echo Dependencies installed
echo.

REM Create .env file if it doesn't exist
if not exist .env (
    echo Creating .env file...
    copy .env.example .env >nul
    echo .env file created
    echo.
    echo WARNING: You need to configure your Azure credentials!
    echo.
    echo Please edit the .env file and add:
    echo   1. AZURE_SPEECH_KEY ^(from Azure Portal^)
    echo   2. AZURE_SPEECH_REGION ^(e.g., eastus^)
    echo.
    pause
) else (
    echo .env file already exists
)
echo.

REM Create upload directory
echo Creating upload directory...
if not exist static\uploads mkdir static\uploads
echo Upload directory created
echo.

REM Summary
echo ==========================================
echo Setup Complete!
echo ==========================================
echo.
echo Next steps:
echo   1. Edit .env file with your Azure credentials
echo   2. Activate environment: venv\Scripts\activate
echo   3. Run application: python app.py
echo   4. Open browser: http://localhost:5000
echo.
echo Quick commands:
echo   - Activate environment:  venv\Scripts\activate
echo   - Deactivate environment: deactivate
echo   - Run app:               python app.py
echo.
echo For help, see README.md or QUICKSTART.md
echo.

REM Check if Azure credentials are configured
findstr /C:"your_subscription_key_here" .env >nul 2>&1
if not errorlevel 1 (
    echo WARNING: Azure credentials not configured!
    echo Please edit .env file before running the application.
    echo.
) else (
    echo Azure credentials appear to be configured
    echo.
    set /p start_app="Would you like to start the application now? (y/n): "
    if /i "!start_app!"=="y" (
        echo.
        echo Starting application...
        python app.py
    )
)

pause
