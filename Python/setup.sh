#!/bin/bash

# Setup script for Azure Speech-to-Text Python application
# This script automates the initial setup process

set -e  # Exit on error

echo "=========================================="
echo "Azure Speech-to-Text Setup Script"
echo "=========================================="
echo ""

# Check Python version
echo "Checking Python version..."
if ! command -v python3 &> /dev/null; then
    echo "? Error: Python 3 is not installed"
    echo "Please install Python 3.9 or higher from https://www.python.org"
    exit 1
fi

PYTHON_VERSION=$(python3 --version | cut -d' ' -f2 | cut -d'.' -f1-2)
echo "? Found Python $PYTHON_VERSION"

# Create virtual environment
echo ""
echo "Creating virtual environment..."
if [ -d "venv" ]; then
    echo "??  Virtual environment already exists"
    read -p "Do you want to recreate it? (y/n): " recreate
    if [ "$recreate" = "y" ]; then
        rm -rf venv
        python3 -m venv venv
        echo "? Virtual environment recreated"
    else
        echo "??  Using existing virtual environment"
    fi
else
    python3 -m venv venv
    echo "? Virtual environment created"
fi

# Activate virtual environment
echo ""
echo "Activating virtual environment..."
source venv/bin/activate
echo "? Virtual environment activated"

# Upgrade pip
echo ""
echo "Upgrading pip..."
pip install --upgrade pip > /dev/null 2>&1
echo "? pip upgraded"

# Install dependencies
echo ""
echo "Installing dependencies..."
echo "This may take a few minutes..."
pip install -r requirements.txt > /dev/null 2>&1
echo "? Dependencies installed"

# Create .env file if it doesn't exist
echo ""
if [ ! -f ".env" ]; then
    echo "Creating .env file..."
    cp .env.example .env
    echo "? .env file created"
    echo ""
    echo "??  IMPORTANT: You need to configure your Azure credentials!"
    echo ""
    echo "Please edit the .env file and add:"
    echo "  1. AZURE_SPEECH_KEY (from Azure Portal)"
    echo "  2. AZURE_SPEECH_REGION (e.g., eastus)"
    echo ""
    read -p "Press Enter to continue..."
else
    echo "??  .env file already exists"
fi

# Create upload directory
echo ""
echo "Creating upload directory..."
mkdir -p static/uploads
echo "? Upload directory created"

# Summary
echo ""
echo "=========================================="
echo "? Setup Complete!"
echo "=========================================="
echo ""
echo "Next steps:"
echo "  1. Edit .env file with your Azure credentials"
echo "  2. Activate environment: source venv/bin/activate"
echo "  3. Run application: python app.py"
echo "  4. Open browser: http://localhost:5000"
echo ""
echo "Quick commands:"
echo "  • Activate environment:  source venv/bin/activate"
echo "  • Deactivate environment: deactivate"
echo "  • Run app:               python app.py"
echo "  • View logs:             tail -f app.log"
echo ""
echo "For help, see README.md or QUICKSTART.md"
echo ""

# Check if Azure credentials are configured
if [ -f ".env" ]; then
    if grep -q "your_subscription_key_here" .env; then
        echo "??  WARNING: Azure credentials not configured!"
        echo "Please edit .env file before running the application."
    else
        echo "? Azure credentials appear to be configured"
        echo ""
        read -p "Would you like to start the application now? (y/n): " start_app
        if [ "$start_app" = "y" ]; then
            echo ""
            echo "Starting application..."
            python app.py
        fi
    fi
fi
