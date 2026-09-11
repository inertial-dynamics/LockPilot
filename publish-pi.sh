src="LockPilot/bin/Release/net10.0/publish/linux-arm64"

if [ -n "$1" ]; then
  scp -r "$src/." "$1:~/LockPilot"
else
  echo "Usage: $(basename "$0") user@host"
fi

read -p "Press Enter to continue..."
