"""
Quick launcher for the hardware simulator.
Usage:
    python run_sim.py                      # Use default config.json (10 devices, 10ms)
    python run_sim.py -d 100              # 100 devices
    python run_sim.py -d 50 -i 5          # 50 devices, 5ms interval
    python run_sim.py -d 100 -H 192.168.1.50  # remote server
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