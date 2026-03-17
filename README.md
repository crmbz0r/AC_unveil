# AiComi Lua Mod

A BepInEx plugin for AiComi that enables Lua scripting support and provides various quality-of-life features.

## Features

- **Lua Console**: Interactive console for executing Lua code during gameplay
- **Some Nice Cheats as a Bonus, including:**
  - **Touch Item Unlocker**: Unlock all cursor items in the massage / "prank" scene for unrestricted... pranking.
  - **Touch Scene No Dislike**: Touch 'em wherever you want, how often you want. (random rejection may still occur depending on personality rarely)
  - **Touch Scene Next H**: Show the "I want ȘΞХ" button immediately, no need to complete two massage sessions if you don' want to.
  - **Cheat Hooks**: Various gameplay cheats and modifications
  - **Dialog Scene Hooks**: Enhanced dialog scene functionality

## Installation

### Prerequisites

- AiComi game installed
- BepInEx installed in your AiComi game directory

### Quick Installation (Recommended)

1. Download the latest release zip file from the releases page
2. Extract the contents to your AiComi game directory
3. Run `install.bat` to automatically install the plugin
4. The plugin will be automatically loaded when you start the game

### Manual Installation (Development)

If you're building from source:

1. Build the project: `dotnet build`
2. Run the installer script: `.\install_plugin.ps1`
3. Follow the prompts to specify your AiComi game directory

### Manual Installation (Zip File)

If you downloaded the release zip file and want to install manually:

1. Extract the zip file contents
2. Copy `BepInEx\plugins\AiComi_LuaMod.dll` to your game's `BepInEx\plugins\` folder
3. Copy `BepInEx\plugins\AiComi_LuaMod.lua` to your game's `BepInEx\plugins\` folder
4. Copy the entire `BepInEx\plugins\lua_scripts\` folder to your game's `BepInEx\plugins\` folder
5. The plugin will be automatically loaded when you start the game

## Usage

### Lua Console

The plugin provides an interactive Lua console that can be accessed during gameplay. Use it to:

- Execute Lua code on the fly
- Test Lua scripts
- Debug game functionality
- Modify game behavior in real-time

### Touch Item Unlocker

To unlock all touch items via Lua:

```lua
UnlockTouchItems(true)
```

To lock them again:

```lua
UnlockTouchItems(false)
```

### Creating Lua Mods

Place your Lua mod files in the `BepInEx/plugins/lua_scripts/mods/` directory. The plugin will automatically load and execute them.

Example mod structure:

```
BepInEx/plugins/lua_scripts/mods/
├── my_mod.lua
├── another_mod.lua
└── ...
```

## Development

### Building from Source

1. Clone this repository
2. Ensure you have .NET 6.0 SDK installed
3. Build the project: `dotnet build`
4. Run the installer: `.\install_plugin.ps1`

### Project Structure

- `AC_LoadModDLL/` - Main plugin project
- `AC_LuaContent/` - Lua scripts and content
  - `mods/` - Example Lua mods
  - `stubs/` - Lua stubs for development

### Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Submit a pull request

## Troubleshooting

### Plugin Not Loading

- Ensure BepInEx is properly installed
- Check that the DLL is in the correct location
- Verify game compatibility

### Lua Scripts Not Working

- Check the Lua console for error messages
- Ensure your Lua syntax is correct
- Verify that required game functions are available

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Support

For support and questions:

- Create an issue on GitHub
- Check the documentation
- Review existing Lua mods for examples
