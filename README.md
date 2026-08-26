# WinKey

WinKey is a Windows 10/11 WPF utility for collecting information before reinstalling Windows.

## Features

- Recover the installed Windows product key from `DigitalProductId`
- Read an OEM/UEFI embedded Windows key when available
- Show Windows edition, version, build, Product ID and install date
- Show activation state
- Show manufacturer, model, CPU, RAM, motherboard, BIOS, GPU and disks
- Show active network adapters, IP addresses and MAC addresses
- Copy the complete report
- Export TXT and JSON reports

## Build

1. Open `WinKey.sln` in Visual Studio 2022.
2. Allow NuGet to restore packages.
3. Build and run the `WinKey` project.

Target framework: .NET 8 for Windows.

## Notes

Some Windows PCs activate with a digital licence tied to the hardware or Microsoft account. In that case, the recovered 25-character key may not be the licence actually used for activation. Keep exported reports private because they can contain product keys, serial numbers and MAC addresses.
