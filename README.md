# TLKAC-Printer-Upload-Service
This Windows service is designed specifically to upload raw .SPD files a Windows machine generates when printing. Then it uploads those files to Firebase. Also, it's my first foray into creating Windows services and it's good C# practice.

## Inspiration
So after being fed up with the point of sales system at my night job (as a restaurant employee), I've decided to do something about it.

## Goals
- Practice some C#
- Learn how to make a Windows service
- Get those order slips the POS prints into the cloud (and then pull them down on my phone in a future project)
- Build a working version in one day

## Things I Learned
- Creating Windows services
- Some basics about Windows services
- Locking properties in a multithreaded environment in C#
- Creating installers for Windows services using Visual Studio
- The FileSystemWatcher class and its limitations
- Basic C# event handlers
- Basic C# file IO
- Windows Event Logger

## How It Works
- For the printer of interest, find it in Control Panel, right click it, select "Printer Properties", click the "Advanced" tab, do whatever it takes to check "Keep Printed Documents", click apply
- Now every document that is sent to the printer is spooled in C:/Windows/System32/spool/PRINTERS
- This service monitors that directory for .SHD (printer header files) and .SPD files (document data for printers to print)
- Every time the directory is written to, FileSystemWatcher triggers an event
- On the event trigger, a snapshot of the directory is taken and all files in the directory are queued
- A new thread is created that will handle one file in the queue at a time
- If a file is a .SHD file, this service just deletes it and moves on
- If a file is a .SPD file, this service uploads it to Firebase
- All other filetypes are ignored
