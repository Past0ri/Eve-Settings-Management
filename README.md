# Eve Settings Management

A small desktop tool for copying **EVE Online** character settings from one character profile to one or more.

## What It Does
- Finds your EVE settings folder automatically.
- Lists available character profiles from `core_char_*` files.
- Copies settings from one selected character to selected target characters.
- Optional backup before copy.

## Requirements
- Windows
- .NET SDK 10.0+

## Run
```powershell
dotnet restore
dotnet run --project "Eve Settings Management.csproj"
```
## Basic Usage
1. Start the app.
2. Confirm or choose your EVE settings folder.
3. Select one character in **Copy From**.
4. Select one or more characters in **Copy To**.
5. (Optional) Enable **Backup**.
6. Click **Copy**.

## Notes
- Character names are fetched from EVE ESI; if unavailable, the app falls back to `Character <id>`.
- Keep EVE closed while copying settings to avoid conflicts.
