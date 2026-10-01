"""
Quick launcher for the hardware simulator.
Usage:
    python run_sim.py                      # Use default config.json
    python run_sim.py -d 100               # 100 devices (100 TCP connections)
    python run_sim.py -n 16                # 16 channels/device -> boards 1 AND 2
    python run_sim.py --board-base 0       # field topology: 1-0-1 .. 1-0-8
    python run_sim.py -d 50 -i 5           # 50 devices, 5ms interval
    python run_sim.py -d 100 -H 192.168.1.50  # remote server

Note: -d scales DEVICES (one TCP connection each). Secondary boards come from
-n/--channels, which rolls into the next board every 8 channels - not from -d.
"""

import subprocess
import sys
import os

def main():
    args = [
        sys.executable,
        "simulator.py"
    ] + sys.argv[1:]

    print("=" * 60)
    print("  BATTERY TESTING SYSTEM - HARDWARE SIMULATOR")
    print("=" * 60)
    print(f"  Command: {' '.join(args)}")
    print("=" * 60 + "\n")

    try:
        subprocess.run(args, cwd=os.path.dirname(os.path.abspath(__file__)))
    except KeyboardInterrupt:
        print("\nSimulator stopped.")

if __name__ == "__main__":
    main()