# kanimal SE (KSE)

- [kanimal SE (KSE)](#kanimal-se--kse-)
  * [Features](#features)
  * [Installing](#installing)
    + [Which file to download](#which-file-to-download)
  * [Usage](#usage)
    + [kanim → scml](#kanim---scml)
      - [Batch conversion](#batch-conversion)
    + [scml → kanim](#scml---kanim)
    + [Arbitrary directions](#arbitrary-directions)
    + [Kanim dump](#kanim-dump)
  * [Things to know](#things-to-know)
    + [Spriter](#spriter)

A library and command-line interface (CLI) converter between cut-out animation formats, including kanim (Klei animation) and scml (Spriter) with a common, intermediate in-memory format. KSE was developed to support *Oxygen Not Included* modding, and therefore features will focus on those that are useful for ONI.

KSE is, at its base, a port of kparserX from Java to C#, and wouldn't have been possible without @daviscook477's work on kparser.

## Features

* Multi-directional conversion between compatible formats: kanim → scml, scml → kanim, kanim → kanim, scml → scml are all possible.
* Support for most Spriter features for making custom ONI animations. Most notably - handles interpolation between keyframes with the `-i` flag.
* Cross-platform executable
* Actively maintained

## Installing

KSE is a dotNET application. This means that you don't need to install the program. Just download the appropriate version [from here](https://github.com/skairunner/kanimal-SE/releases). Below is a quick guide to which version is right for you.

### Which file to download

There are three supported operating systems (Windows, MacOS and Linux) and two types of packaging (dotNET dependent and self-contained), meaning six downloads possible. **If you already have dotNET v3 or higher installed** on your computer, use the NET Dependent download to have a much smaller file size (631 kb vs 29MB). If you don't have dotNET installed, or don't want to risk the program not working, I recommend downloading the self-contained version instead.  

## Usage

KSE is a command line tool. This means that you need a terminal to run the program, via typing in the magic incantations.

Generally, there are two ways to invoke all of KSE's conversion features: the shortcut or via the `convert` verb. Below is a quick, step-by-step guide to converting anims.

### kanim → scml

1. You need to have "loose" kanim files. That is, you should have three files ready: `[NAME].png`, `[NAME]_anim.bytes`, and `[NAME]_build.bytes`. ONI stores these files in its `[steam directory]/OxygenNotIncluded_Data/sharedassets#.assets` files. Use a tool such as [uTinyRipper](https://github.com/mafaca/UtinyRipper) to unpack  the files. The three kanim files you need should be located in the TextAssets and Texture2D directories. It is highly recommended to move all the files to the same directory.
2. Open a terminal of your choice and navigate to the directory with your kanim files. On Windows, it is usually `cmd`.
3. The parameters for invoking KSE are:
```
# on windows
$ kanimal-cli.exe scml [NAME].png [NAME]_anim.bytes [NAME]_build.bytes
# on mac/linux
$ ./kanimal-cli scml [NAME].png [NAME]_anim.bytes [NAME]_build.bytes
```
The files can be provided in any order. On many consoles, it is possible to specify a file by dragging it into the terminal, or copy and pasting the path directly. If no issues are encountered, this command should output the scml file and all unpacked sprites into the `output/` directory, relative to where you run the program.

To specify a directory, you can use the `-o/--output` switch:
```
$ kanimal-cli.exe scml [NAME].png [NAME]_anim.bytes [NAME]_build.bytes -o my/output/path
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
$ kanimal-cli.exe kanim [NAME].scml
# on mac/linux
$ ./kanimal-cli kanim [NAME].scml
```

Currently the ability to use Spriter's interpolation between keyframes features is opt-in. To use it you must use the `-i/--interpolate` switch.
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
| `-i/--interpolate` | Interpolate SCML files on load. This means that all in-between frames are generated from the keyframes. |
|`-S/--strict` | Enforce strict conversion.|
|`-f/--strictly-order-files` | Instead of inferring the file types from the extensions, require that files be provided in png, build, anim order | 

### Kanim dump
KSE supports dumping the contents of a kanim file to a (relatively) readable text file. The command is:
```
$ kanimal-cli dump [FILES...]
```

## Things to know

### Spriter
* You cannot change the pivot point from frame to frame. However, you *can* change the pivot for the entire sprite from the right side Pallate menu. If you change the pivot point in a specific frame that change will not be respected by the converter. It will throw a warning for you to indicate that this is not allowed. This will be changed to an error in strict conversion mode.
* Spriter's intrinsic interpolation is supported if you use the `-i/--interpolate` option. Currently the `-i` option works best if you keyframe the start and the end frame. For example if you want a 30 frame animation that you should have length 33ms * 30 = 990ms where you must key 0ms and 990ms in order for interpolation to work. If you have a use case in which providing key frames on the end of animation is not sufficient or where interpolation seems to fail please open an issue.
* Each sprite must have an underscore and a number at the end of their name, but before the file extension. The number should start at 0 and be sequential. So, if you have three sprites called "blob", they should be respectively named `blob_0.png`, `blob_1.png`, and `blob_2.png`. Each name before the underscore with number is considered an individual *symbol* which means an object of sorts in the animation that can change between sprites. So going with the `blob` example: `blob` would be the symbol and could be switched between any of the three sprites `blob_0`, `blob_1`, `blob_2`. When you want to change what sprite is used in a certain location in your image use symbols and this [link](http://www.brashmonkey.com/spriter_manual/swapping%20the%20image%20of%20a%20sprite.htm) to see how to switch the sprites for that object. This keeps all the sprite changes on just a single timeline rather than needing multiple timelines for a single part just because it has sprite changes.
* Spriter's sprite swapping feature only works when swapping between sprites of the same symbol. So going from `blob_0` to `blob_1` or `blob_2` is fine but going from `body_0` to `head_0` will probably break.
* All frames in a given animation should be spaced at the same interval. Klei uses 33ms spacing between frames by default, but any interval will do. Enabling snapping is highly recommended. If there isn't a consistent interval, the converter will throw an error.
![magnet tool location](https://raw.githubusercontent.com/skairunner/kparserX/master/imgs/timeline_settings_buttons.png)
![example of enable snapping](https://raw.githubusercontent.com/skairunner/kparserX/master/imgs/timeline_settings_enable_snapping.png)
    * Note that this is also valid, because even if frames are missing, the interval between each frame is consistent:
    ![example of consistent interval that doesn't equal snapping interval](https://user-images.githubusercontent.com/3517115/68343547-4927b200-00aa-11ea-84df-509ffcd7fcb3.png)
    This animation will be compiled as if all frames were snapped to 132ms, and were each 132ms long.
