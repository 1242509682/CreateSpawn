using Microsoft.Xna.Framework;
using Terraria;

namespace CreateSpawn;

public class Building
{
    //建筑的名称
    public string name { get; set; }

    //建筑的原始图格数据
    public Dictionary<Point, Tile> OrigTiles = new Dictionary<Point, Tile>();

    public byte bTileHeader { get; set; }
    public byte bTileHeader2 { get; set; }
    public byte bTileHeader3 { get; set; }
    public short frameX { get; set; }
    public short frameY { get; set; }
    public byte liquid { get; set; }
    public ushort sTileHeader { get; set; }
    public ushort type { get; set; }
    public ushort wall { get; set; }

    public Building() { }

    public Building(string name, byte header, byte header2, byte header3, short x, short y, byte liquid, ushort sHeader, ushort type, ushort wall, Dictionary<Point, Tile> origTiles)
    {
        this.name = name;
        this.bTileHeader = header;
        this.bTileHeader2 = header2;
        this.bTileHeader3 = header3;
        this.frameX = x;
        this.frameY = y;
        this.liquid = liquid;
        this.sTileHeader = sHeader;
        this.type = type;
        this.wall = wall;
        this.OrigTiles = origTiles;
    }
}
