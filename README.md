# Modding a Unity game for bhaptics support

This is a mod for the very old game "Shadow Legend VR". I cleaned up the code a bit so it can be used as an example how to go about modding a Unity game to support bhaptics.

## Modding rules

1. Ideally, talk to the developers first. They usually have a Discord and are really nice. They might even have plans to add bhaptics anyways. Most of them won't have a problem with mods in general, and bhaptics support in particular. But if they really, really hate mods, they might actually get mad and come after you. For modding, you do need to disassemble their code and hook into their functions. Which is not per se illegal, but usually against the terms of service, and they might try to block your account or game if they are evil...

2. Don't talk about it too publicly. For a single player experience, this shouldn't be a big problem, but in multiplayer games, it is usually really easy to cheat once you started modding. So think about how public you want your code to be and such.

3. Check if the game already has a modding community and what kind of mod loader they use for functional mods like this one. Maybe it's not MelonLoader but BepInEx, which also works fine, and you can find an example of mine in the [H3VR mod](https://github.com/floh-bhaptics/H3VR_bhaptics).

## How to get started

1. Install MelonLoader on your game executable. Download the installer from [here](https://melonwiki.xyz/#/?id=requirements) and select your game. Run the game once with MelonLoader installed. To make sure you don't run into problems and to get some info on the game name etc.

2. Inside the game folder, you should now look for the `Assembly-CSharp.dll` file. This is usually either in some `GameName_data\Managed\` folder (for Mono games) or in the `MelonLoader\Managed\` folder (for il2cpp games, where MelonLoader did some heavy lifting for you). You can later link to this file directly, and this will work for most newer games (.NET >= 4.0), but for older games or to make sure, you can copy this `.dll` and others you might need (like the `MelonLoader.dll` or the `UnityEngine.dll` from this `Managed` folder into an extra "libs" folder in your project later.

3. Disassemble this `Assembly-CSharp.dll` file. I usually use [dotPeek](https://www.jetbrains.com/de-de/decompiler/), but any decompiler will do. The decompiler will also tell you what .NET version the game is built on, which you will need in the next step. It is a good idea to now check if the decompilation worked fine and if it looks like you can find good functions to hook into in the game.

4. Time to create your project. I use [Visual Studio](https://visualstudio.microsoft.com/de/vs/community/) for this, be sure to get the community edition for legal reasons. Create a new project, and select a .NET library class. Pick the .NET version according to your disassembled file, either 3.5 or the newest 4 (currently 4.7.2).

5. Some things I do at the very start:
  - Rename the class file and name.
  - Add references to the libraries you need, definitely MelonLoader and the `Assembly-CSharp.dll`, ideally from a `libs` directory in your repo that you copy them into. Make sure to set the "local copy" setting to "false" because you don't need those DLLs in your build later, they are in the game already.
  - Edit the `AssemblyInfo.cs` and add `using MelonLoader;` and its assembly info, especially the game and vendor name you can get from the `Latest.log` that MelonLoader created.
  - I usually set the built output directory to the `Mods` directory of the game to avoid copying later on.
  - Add a class for the bHaptics suit communication. I usually just copy-paste the `MyBhapticsTactsuit.cs` I already have and adapt it later if needed.

6. Add the base functionality of the mod to your main library class file. Something like
```
namespace ShadowLegend_bhaptics
{
    public class ShadowLegend_bhaptics : MelonMod
    {
        public static TactsuitVR tactsuitVr;

        public override void OnApplicationStart()
        {
            base.OnApplicationStart();
            tactsuitVr = new TactsuitVR();
            tactsuitVr.PlaybackHaptics("HeartBeat");
        }
	}
}

```

7. In the `Mods` directory in your game, add a `bHaptics` folder and place the `HeartBeat.tact` file in there, to have something to start with.

8. Now you can compile it, make sure the `Mods` directory in your game looks okay and contains your mod DLL and the bHaptics directory. Start the bhaptics Player, connect your vest and run the game. And hope for a heartbeat. ;-)

9. If this works fine, you can start to search through the disassembled game file for functions that you can hook into to give feedback in the gear. You will get to know the game's code structure fairly well during the modding process, so really dive into it first. For some example functions, you can check the code in this repository, even though it is not clean and only writtern quickly and for myself... I think these are the sites I used to get started with HarmonyLib:
  - [MelonLoader/Harmony Patching](https://github.com/TDToolbox/BTD-Docs/blob/master/Unity%20Engine/MelonLoader/Harmony%20Patching.md)
  - [Harmony basics](https://api.raftmodding.com/modding-tutorials/harmony-basics)

## Testing and publishing the mod

Make sure your mod is working properly and test it with other people. If you feel like it is good to publish, pick a site and upload it. I use NexusMods, which has gotten a bit of critique lately because modders aren't able to delete their old mods anymore if they are part of a compilation. And you need a (free) account to download mods there. But I haven't found a great alternative yet that doesn't have its own issues like aggressive mod managers or tons of ads everywhere. Oh well.
