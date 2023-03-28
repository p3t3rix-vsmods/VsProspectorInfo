# VsProspectorInfo
A clientside only mod to save trees by not having to write down the values of your prospecting.
## Installation
Just drop the file into your mods folder. You just need this mod on the client, no serverside installation needed.


## Commands

    .pi - main command for the mod and the default sub-command is to 'showoverlay'
    .pi showoverlay [true,false] - Show or hide the overlay on the map. Toggles without argument.
    .pi showborder [true,false] - Show or hide the border around chunks. Toggles without argument.
    .pi showgui [true,false] - Show the GUI where you can configure the mode (default or heatmap) and select the ore that should be heatmapped.
    .pi setcolor (overlay|border|lowheat|highheat) [0-255] [0-255] [0-255] [0-255] - Sets the color of the respective element.
    .pi setborderthickness [1-5] - Sets the border thickness. 
    .pi mode [0-1] - Sets the map mode. Supported modes: 0 (Default) and 1 (Heatmap)
    .pi heatmapore [oreName] - Changes the heatmap mode to display a specific ore.
        No argument resets the heatmap back to all ores. Can only handle the ore name in your selected language or the ore tag.
        Examples: game:ore-emerald, game:ore-bituminouscoal, Cassiterite.
    .pi setsaveintervalminutes [1-60] - Periodically store the prospecting data every x minutes.

Each command updates the respective configuration option.

## Configuration

    TextureColor [0-255] [0-255] [0-255] [0-255] - The default color to use for the overlay. Default: 150 125 150 128
    BorderColor [0-255] [0-255] [0-255] [0-255] - The default color to use for the border. Default: 0 0 0 200
    LowHeatColor [0-255] [0-255] [0-255] [0-255] - Heatmap color for low relative densitry. Default: 85 85 181 128
    HighHeatColor [0-255] [0-255] [0-255] [0-255] - Heatmap color for low relative densitry. Default: 168 34 36 128
    BorderThickness [1-5] - The thickness, in pixels, of the border color. Default: 1
    RenderBorder [true,false] - Whether or not to render the border at all. Default: true
    AutoToggle [true,false] - Whether or not to toggle the overlay on the map automatically, 
                              based on the player equipping/unequipping a prospecting pick. Default: true
    HeatMapOre [oreName] - The ore selected for the heatmap.
    MapMode [0-1] - The mode of the map.
    SaveIntervalMinutes [1-60] - Periodically store the prospecting data every x minutes.

## Usage

Whenever you finish prospecting a chunk, the data is saved into the ModData folder and added to the chunk info of the world map. This is just a 1:1 parsing of the chat message that the prospecting pick sends. 

The mod renders a transparent square of all chunks that have been prospected. If a chunk is re-prospected, the message is simply overwritten. The rendering of these squares can be toggled with the .pi command.

After prospecting, the info will be displayed in the tooltip of the minimap when hovering over the chunk. This info is stored in %Vintage_Story_Data%/ModData/YourWorldId/vsprospectorinfo.data and is client side only.

![image](https://user-images.githubusercontent.com/5238284/79952656-09e3f680-847b-11ea-96c9-b4cb9b47355f.png)

### Heatmap

A map mode that displays the relative density of the ores on the map via a color gradient. Can be enabled/disabled and switched between displaying the density of just one ore and displaying the density of all ores (The highest density per chunk is picked).

Normal map (map mode 0)
![map](https://user-images.githubusercontent.com/24532072/168427928-96b134aa-288d-4d4c-ade6-ddcb002c6d51.png)


Heatmap (map mode 1)
![heatmap](https://user-images.githubusercontent.com/24532072/168427930-571788d3-eca5-4cbb-b6d6-caf2c6b9bcd1.png)


Heatmap for Cassiterite only (map mode 1; heatmapore Cassiterite)
![heatmapCassiterite](https://user-images.githubusercontent.com/24532072/168427932-9fd7020f-3248-4708-8f68-25a082a86bd2.png)



## Compiling
Clone the repository with submodules included: "git clone --recursive"
To compile the mod you also need to set 2 environment variables:
- VINTAGE_STORY => the path to the game directory e.g. c:\games\vintagestory
- VINTAGE_STORY_DATA => the path to the games data directory typically located somewhere in appdata e.g. C:\Users\MyUser\AppData\Roaming\VintagestoryData

## Create a release
To create a release just compile the solution in Release configuration. A folder named "release" should appear in the solution directory.
This can then be zipped to be uploaded to the mod-db.
