# kanimal SE (KSE)

A library and command-line interface (CLI) converter between cut-out animation formats, including kanim (Klei animation) and scml (Spriter) with a common, intermediate in-memory format. KSE was developed to support *Oxygen Not Included* modding, and therefore features will focus on those that are useful for ONI.

KSE is, at its base, a port of kparserX from Java to C#, and wouldn't have been possible without @daviscook477's work on kparser.

## Features

* Multi-directional conversion between compatible formats: kanim → scml, scml → kanim, kanim → kanim, scml → scml are all possible.
* Cross-platform executable
* Actively maintained

## Usage

KSE is a command line tool. This means that you need a terminal to run the program, via typing in the magic incantations.

Generally, there are two ways to invoke all of KSE's conversion features: the shortcut or via the `convert` verb. Below is a quick, step-by-step guide to converting anims.

### kanim → scml

1. You need to have "loose" kanim files. That is, you should have three files ready: `[NAME].png`, `[NAME]_anim.bytes`, and `[NAME]_build.bytes`. ONI stores these files in its `[steam directory]/OxygenNotIncluded_Data/sharedassets#.assets` files. Use a tool such as [uTinyRipper](https://github.com/mafaca/UtinyRipper) to unpack  the files. The three kanim files you need should be located in the TextAssets and Texture2D directories. It is highly recommended to move all the files to the same directory.
2. Open a terminal of your choice and navigate to the directory with your kanim files. On Windows, it is usually `cmd`.
3. The parameters for invoking KSE are: 
```
# on windows
$ kanimal-cli.exe kanim [NAME].png [NAME]_anim.bytes [NAME]_build.bytes
# on mac/linux
$ ./kanimal-cli kanim [NAME].png [NAME]_anim.bytes [NAME]_build.bytes
```
The files can be provided in any order. On many consoles, it is possible to specify a file by dragging it into the terminal, or copy and pasting the path directly. If no issues are encountered, this command should output the scml file and all unpacked sprites into the `output/` directory, relative to where you run the program.

To specify a directory, you can use the `-o/--output` switch:
```
$ kanimal-cli.exe kanim [NAME].png [NAME]_anim.bytes [NAME]_build.bytes -o my/output/path
```

#### Batch conversion

It is possible to batch convert *Oxygen Not Included* assets to Spriter files.

1. Unpack the Unity asset bundles. Ensure that the root directory is `Assets`, which contains `Texture2D` and `TextAsset`.
2. Run the following command:
```
$ kanimal-cli batch-convert /path/to/assets/directory
```
3. The result files will be output in the `output/` directory relative to the current working directory. You can specify a different path with the `-o/--output` flag, as always. 

### scml → kanim
The process is very similar to the previous one.

1. You need to have a Spriter project file (extension `.scml`), as well as all sprites required by the project. Any unused sprites or missing sprites might cause an error message. (Note: Currently, KSE only checks sprites in the root directory for inclusion. If your project uses a nested file structure, please inform the maintainers so we can implement that feature)
2. Open a terminal and navigate to the directory with your project files.
3. The parameters for invoking KSE are:
```
# on windows
$ kanimal-cli.exe scml [NAME].scml
# on mac/linux
$ ./kanimal-cli scml [NAME].scml
```

Just like in the kanim → scml case, the files are output by default into the `output/` directory, and a specific path can be specified with the `-o/--output` switch.

### Arbitrary directions
1. Reference the above guides for the files required.
2. The parameters for invoking KSE are:
```
$ ./kanimal-cli convert -I [INPUT_FORMAT] -O [OUTPUT_FORMAT] [FILES ...]
```

Other available switches are as follows:  

| switch | effect |
|--------|--------|
| `-o/--output` | Specify an output directory |
| `-v/--verbose` | Set verbosity level to DEBUG (default INFO)|
| `-s/--silent` | Set verbosity level to FATAL (default INFO). This means no messages are logged on successful conversion, including warnings. |
|`-S/--strict` | Enforce strict conversion.|

### Kanim dump
KSE supports dumping the contents of a kanim file to a (relatively) readable text file. The command is:
```
$ kanimal-cli dump [FILES...]
```

## Things to know

### Spriter
* You cannot change the pivot point from frame to frame. However, you *can* change the pivot for the entire sprite from the right side Pallate menu.
* Spriter's intrinsic interpolation is supported.
* Each sprite must have an underscore and a number at the end of their name, but before the file extension. The number should start at 0 and be sequential. So, if you have three sprites called "blob", they should be respectively named `blob_0.png`, `blob_1.png`, and `blob_2.png`.
* All frames in a given animation should be spaced at the same interval. Klei uses 33ms spacing between frames by default, but any interval will do. Enabling snapping is highly recommended.
![magnet tool location](https://raw.githubusercontent.com/skairunner/kparserX/master/imgs/timeline_settings_buttons.png)
![example of enable snapping](https://raw.githubusercontent.com/skairunner/kparserX/master/imgs/timeline_settings_enable_snapping.png)
    * Note that this is also valid, because even if frames are missing, the interval between each frame is consistent:
    ![example of consistent interval that doesn't equal snapping interval](https://user-images.githubusercontent.com/3517115/68343547-4927b200-00aa-11ea-84df-509ffcd7fcb3.png)
    This animation will be compiled as if all frames were snapped to 132ms, and were each 132ms long.