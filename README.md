WPSDB
=====

Overview
--------
WPSDB is a Visual Components plugin that serializes per-robot WPS (WPSDF) data into the project layout and restores it on layout load. It keeps a per-layout buffer file for each robot and synchronizes those WPSDF snippets with persistent layout items so robot WPS data travels with the layout file.

Key features
------------
- Detects robot components (components with BehaviorType.RobotController).
- On layout save: creates/updates layout items named with prefix "KWS_{RobotName}" and stores serialized WPSDF JSON in the property "RobotWpsdfData".
- On layout load: restores RobotWpsdfData into per-robot buffer files and updates RobotSettings.WpsFilePath to point to the buffer file.
- Automatically creates missing buffer directories/files using a default path pattern.

Defaults
--------
- Layout item prefix: `KWS_`
- Default buffer file path pattern: `C:\\Users\\Public\\Documents\\Delfoi\\WPS\\WPS_DataFile_{RobotName}_{LayoutName}.wpsdf`
- Layout item property name: `RobotWpsdfData`
- Robot settings JSON property used: `WpsFilePath` inside `RobotSettings`

Dependencies
------------
- Visual Components SDK (IPlugin, IApplication, ISimWorld, ILayoutPropertyList, etc.)
- Caliburn.Micro
- Newtonsoft.Json

Build
-----
- Open WPSDB.slnx in Visual Studio (project targets .NET Framework 4.7.2).
- Restore NuGet packages and build the solution.
- Output: UX.WPSDB.dll

Install / Deploy
----------------
- Copy the built UX.WPSDB.dll (and any dependent assemblies if required) into the Visual Components add-ins/plugins folder or register the plugin according to your Visual Components installation procedure.
- Restart Visual Components. The plugin is exported with MEF (Export(typeof(IPlugin))) and will be loaded automatically.

Configuration
-------------
To change naming or default path behavior, edit constants in WPSDB.cs:
- `LAYOUT_ITEM_NAME`
- `DEFAULT_WPSDF_PATH`
- `WPSPATH_END`

Behavior notes
--------------
- If a robot component's `RobotSettings.WpsFilePath` points to a non-existent file, the plugin will create a default buffer file and notify the user.
- WPSDF is only persisted when serialized JSON is not empty ("{}" is treated as no data).
- Errors are logged through Visual Components IMessageService.

Troubleshooting
---------------
- No robots detected: ensure robot components include a RobotController behavior.
- WPS data missing: verify the layout contains items starting with `KWS_` and that they have a `RobotWpsdfData` property.
- File permission issues: ensure write access to the default buffer folder or change `DEFAULT_WPSDF_PATH`.

License
-------
- Apache-2.0.

Author
------
Noskov N.V. AO Romanov

Assembly metadata is available in `WPSDB/Properties/AssemblyInfo.cs`.
