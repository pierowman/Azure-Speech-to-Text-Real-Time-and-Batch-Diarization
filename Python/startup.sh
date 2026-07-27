#!/usr/bin/env bash
# Startup script for Azure App Service (Linux / Oryx).
#
# Oryx copies this script to /opt/startup/startup.sh, activates the built
# virtual environment (antenv) and sets PYTHONPATH before running it, so we do
# NOT hardcode /home/site/wwwroot or re-activate the venv here.
#
# The Azure Speech SDK (azure-cognitiveservices-speech) requires the native
# ALSA library libasound2, which is not present in the default App Service
# Python image. Install it before starting the app. App Service runs the
# startup command as root, so apt-get is available.
set -e

# Mirror all output to a persistent log under /home/LogFiles so startup issues
# (apt, gunicorn, Python import errors) can be inspected via the SCM VFS API,
# even when the container's stdout pipe is not captured.
mkdir -p /home/LogFiles
exec > >(tee -a /home/LogFiles/startup_custom.log) 2>&1
echo "[startup] ===== $(date -u) starting ====="

echo "[startup] Installing native dependency libasound2 for Azure Speech SDK..."
apt-get update && apt-get install -y --no-install-recommends libasound2 \
  || echo "[startup] WARNING: libasound2 install failed; Speech SDK import may fail."

echo "[startup] python: $(command -v python) ; gunicorn: $(command -v gunicorn)"
echo "[startup] Verifying Speech SDK import..."
python -c "import azure.cognitiveservices.speech as s; print('[startup] Speech SDK import OK', s.__version__)" \
  || echo "[startup] ERROR: Speech SDK import FAILED (see traceback above)."

echo "[startup] Launching gunicorn..."
if command -v gunicorn >/dev/null 2>&1; then
  exec gunicorn --bind=0.0.0.0 --timeout 600 --workers 2 app:app
else
  # Fall back to the module form; Oryx sets PYTHONPATH to the venv site-packages.
  exec python -m gunicorn --bind=0.0.0.0 --timeout 600 --workers 2 app:app
fi

