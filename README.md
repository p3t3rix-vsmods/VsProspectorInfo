# VsProspectorInfo
A clientside only mod to save trees by not having to write down the values of your prospecting.
## Installation
Just drop the file into your mods folder. You just need this mod on the client, no serverside installation needed.


## Commands

    .pi - main command for the mod and the default sub-command is to 'showoverlay', which will toggle the rendering of the texture on the map
    .pi showoverlay [true,false] - Sending no arguments will simply toggle the value of the RenderTexturesOnMap config option. Sending either true or false, will set the config option to the appropriate value
    .pi setcolor [0-255] [0-255] [0-255] [0-255] - Set's the value of the TextureColor field in the config & rebuilds the texture.
    .pi setbordercolor [0-255] [0-255] [0-255] [0-255] - Set's the value of the BorderColor field in the config & rebuilds the texture.
    .pi setborderthickness [number] - Set the BorderThickness value in the config & rebuilds the texture.
    .pi toggleborder [true,false] - Set's the `RenderBorder` value in the config & rebuilds the texture.
    .pi showgui - Shows the GUI where you can configure the mode (default or heatmap) and select the ore that should be heatmapped

## Configuration

    TextureColor [0-255] [0-255] [0-255] [0-255] - The default color to use for the texture. Default: 7 52 91 50
    BorderColor [0-255] [0-255] [0-255] [0-255] - The default color to use for the border texture. Default: 0 0 0 200
    BorderThickness [number] - The thickness, in pixels, of the border color. Default: 1
    RenderBorder [true,false] - Whether or not to render the border at all. Default: true
    AutoToggle [true,false] - Whether or not to toggle the texture on map automatically, based on the player equipping/unequipping a prospecting pick. Default: true

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
