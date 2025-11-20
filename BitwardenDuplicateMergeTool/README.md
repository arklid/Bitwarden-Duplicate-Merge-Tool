# Bitwarden Duplicate Merge Tool

                                           ▄▄▄▄▄▄▄▄
                               ▄▄▄▀▀▀█    █▀ ▄▄▄▄ █
               ▄▄▄▄▄▄         ▄▀ ▄▄█ █    █ ▓███  ▀▀█  ▄▄▄▄▄▄▄▄
          ▄▄▄█▀▀ ▄▄ ▀▀█▄  ▄▄▄▄█ ▓███ █▄▄▄█▌▐███▌ ▀▀ ▀█ █ ▄▄▄▄ █▄
     ▄ ▄█▀▀ ▄▄▄██▀█▓▓▄ ██▀▀ ▄▄  ▀▓██▌▀ ▄▄▄ ▓███ ▐███ █▄█▌▐███▌ ▀▀▀▀ ▀
         ▄▓▓██▀  ▐██▓▌ ▀▄▄██▀██▓▄▀██▌ ▓██ ▄███▌ ▓██▌  ▄▄▄▄███▓ !fa
         ▐▓██▌ ▀▀████ ▓▓██▀ ▐██▓▌▐███▄██▄ ▐███ ███▌ ▄███▀ ▐██▓▌
      ▄   ▓███ ░▐███▌▀▓██▌ ▄███▀ ███ ▀██▓▓ ██▌ ███ ▐▓██▌ ▄██▓▓▀ ▀
        █ ▐███▌ ▀▀▀▀  ▓███  ▀▀  ███▌  ▐██▓▌▓██ ▀ ▄ ▀▓██▄██▀▀  ▄▄▀
        ▀█ ▀▀▀▀ ▄█▀▀█ ▐███▌ ▀  ▄███▌▀  ███▓ ▀▀▀▀ ▄▄▄  ▀▀  ▄▄▄█▀
         ▀▀▀▀▀▀▀▀   ▀█ ▀▀▀▀ █▄▄ ▀▀▀ ▄ ▐███▌ ▄█▀▀▀▀ ▀▀▀▀▀▀▀▀
                     ▀▀▀▀▀▀▀▀ ▀▀▀▀▀▀█ ▀▀▀▀ ▄█ fredric arklid presents
                                    ▀▀▀▀▀▀▀▀
						   Bitwarden Duplicate Merge Tool						

## Overview

This CLI application helps users deduplicate (remove duplicate) entries in their Bitwarden vault by analyzing an exported JSON file and merging or removing duplicate items based on user input.

The reason for creating this MVP application was that I ended up with a vault containing many duplicate entries after exporting / importing multiple sources of data. And after even more fiddeling with various applications that are supposed to solve the issue, i ended up creating my own solution for the problem.

There was no existing tool to help with deduplication in the way I wanted, so I built this simple CLI to address the need that I had.

Feel free to contribute or modify it for your own use cases as long as you respect the original licenses of any source components. 

Currently only supports unencrypted JSON exports.

## Prerequisites
- .NET 8 SDK installed

## Quick start
1. Build:
   dotnet build
2. Run:
   dotnet run --project BitwardenDuplicateMergeTool

When run the app will prompt for the path to a local exported Bitwarden JSON file, analyze duplicates, and optionally save a deduplicated JSON file.

## Usage notes
- The application requires an exported JSON file from the Bitwarden web vault or other trusted tooling and provide its path to this tool.
- The exported vault is plaintext JSON — treat it as sensitive and delete it when done.

## Project files
- [`BitwardenDuplicateMergeTool/Program.cs`](BitwardenDuplicateMergeTool/Program.cs:1) - The main application
- [`BitwardenDuplicateMergeTool/Models/VaultModel.cs`](BitwardenDuplicateMergeTool/Models/VaultModel.cs:1) - POCOs for Bitwarden export JSON schema
- [`BitwardenDuplicateMergeTool/Deduplicator.cs`](BitwardenDuplicateMergeTool/Deduplicator.cs:1) - deduplication and merge logic
- [`BitwardenDuplicateMergeTool/BitwardenDuplicateMergeTool.csproj`](BitwardenDuplicateMergeTool/BitwardenDuplicateMergeTool.csproj:1) - project file

- [`BitwardenDuplicateMergeTool.Tests/DeduplicatorTests.cs`](BitwardenDuplicateMergeTool.Tests/DeduplicatorTests.cs:1) - deduplication unit tests
- [`BitwardenDuplicateMergeTool.Tests/AdditionalTests.cs`](BitwardenDuplicateMergeTool.Tests/AdditionalTests.cs:1) - additional deduplication unit tests
- [`BitwardenDuplicateMergeTool.Tests/BitwardenDuplicateMergeTool.Tests.csproj`](BitwardenDuplicateMergeTool.Tests/BitwardenDuplicateMergeTool.Tests.csproj:1) - unit test project file


## Security & privacy
- Treat the exported JSON as highly sensitive — do not commit, upload, or share it.
- Consider running the tool on a trusted machine and removing any exported files after use.

## Interactive duplicate resolution commands (Preview)
Interactive duplicate-resolution commands have been implemented. When you choose to "Inspect duplicates interactively" in the CLI, the following commands are available for each duplicate group:

- v <idx|list>   — View item details (examples: `v 0`, `v 0-2`, `v 0,2,4`)
- m <list>       — Merge selected items (provide two or more indices, e.g. `m 0,1,2`)
- d <list>       — Delete selected items from the vault (e.g. `d 1` or `d 1-2`)
- k              — Keep first item in the group and remove all others
- s or n         — Skip to the next group (no changes)
- p              — Go to the previous group
- q              — Quit interactive mode immediately

Notes:
- Index lists support comma-separated values and ranges (e.g. `0,2-4`).
- Merges are deterministic and performed by [`BitwardenDuplicateMergeTool/Deduplicator.cs`](BitwardenDuplicateMergeTool/Deduplicator.cs:1) via the MergeItems logic; the interactive loop and command parsing are implemented in [`BitwardenDuplicateMergeTool/Program.cs`](BitwardenDuplicateMergeTool/Program.cs:189).

## Example commands
- dotnet build
- dotnet run --project BitwardenDuplicateMergeTool
- dotnet test

## Todo

Alot, this is a simple application written in a hurry. Feel free to contribute and use as long as credits are given.

## Disclaimer

- The developer is not responsible for any data loss or any problems that may occur from using this software.
- This product is provided "as-is" with no warranties of any kind.
- Make a backup of your data before using this tool; ensure you have copies of any sensitive exports.
- This application may cause harm, including data loss or corruption — use it at your own risk.
- This project is not affiliated with Bitwarden in any way.