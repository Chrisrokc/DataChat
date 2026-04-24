#!/bin/bash
# Interactive uninstall for DataChat. Double-click from /Applications/DataChat.
set -eu

echo "DataChat uninstaller"
read -p "This will stop DataChat and remove /Applications/DataChat. Continue? [y/N] " confirm
[[ "$confirm" =~ ^[Yy]$ ]] || { echo "Aborted."; exit 0; }

USER_ID="$(id -u)"
launchctl bootout "gui/$USER_ID/com.datachat.app" 2>/dev/null || \
  launchctl unload "/Library/LaunchAgents/com.datachat.app.plist" 2>/dev/null || true

sudo rm -f "/Library/LaunchAgents/com.datachat.app.plist"

if [ -f "/Applications/DataChat/sqlserver/docker-compose.yml" ]; then
    echo "Stopping bundled SQL Server container..."
    docker compose -f "/Applications/DataChat/sqlserver/docker-compose.yml" down -v 2>/dev/null || true
fi

read -p "Remove all DataChat data (uploads, logs, keys)? [y/N] " purge
if [[ "$purge" =~ ^[Yy]$ ]]; then
    sudo rm -rf "/Applications/DataChat"
    echo "Removed /Applications/DataChat."
else
    sudo rm -rf "/Applications/DataChat/bin"
    echo "Binaries removed; configuration & data preserved in /Applications/DataChat."
fi

sudo pkgutil --forget com.datachat.app 2>/dev/null || true
echo "Done."
