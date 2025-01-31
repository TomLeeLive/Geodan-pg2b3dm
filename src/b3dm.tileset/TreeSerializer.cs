﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using B3dm.Tileset.Extensions;
using Newtonsoft.Json;
using subtree;
using Wkx;

namespace B3dm.Tileset;

public static class TreeSerializer
{
    public static string ToJson(List<Tile> tiles, double[] transform, double[] region, double[] geometricErrors, double minheight, double maxheight, Version version = null, string refine = "ADD")
    {
        var tileset = ToTileset(tiles, transform, region, geometricErrors, minheight, maxheight, version, refine);
        var json = JsonConvert.SerializeObject(tileset, Formatting.Indented, new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore });
        return json; ;
    }

    public static TileSet ToImplicitTileset(double[] transform, double[] box, double maxGeometricError, int availableLevels, int subtreeLevels, Version version = null, bool createGltf = false)
    {
        var ext = createGltf ? ".glb" : ".b3dm";
        var geometricError = maxGeometricError;
        var tileset = GetTilesetObject(version, maxGeometricError);
        var matrix = SpatialConverter.EcefToEnu(new System.Numerics.Vector3((float)transform[0], (float)transform[1], (float)transform[2]));
        var t = new double[] { matrix.M11, matrix.M12, matrix.M13, matrix.M14, matrix.M21, matrix.M22, matrix.M23, matrix.M24, matrix.M31, matrix.M32, matrix.M33, matrix.M34, matrix.M41, matrix.M42, matrix.M43, matrix.M44 };
        var root = GetRoot(geometricError, t, box);
        var content = new Content() { uri = "content/{level}_{x}_{y}" + ext };
        root.content = content;
        var subtrees = new Subtrees() { uri = "subtrees/{level}_{x}_{y}.subtree" };
        root.implicitTiling = new Implicittiling() { subdivisionScheme = "QUADTREE", availableLevels = availableLevels, subtreeLevels = subtreeLevels, subtrees = subtrees };
        tileset.root = root;
        return tileset;
    }

    public static TileSet ToTileset(List<Tile> tiles, double[] transform, double[] region, double[] geometricErrors, double minheight, double maxheight, Version version = null, string refine="ADD")
    {
        var tileset = GetTilesetObject(version, geometricErrors[0]);

        var matrix = SpatialConverter.EcefToEnu(new System.Numerics.Vector3((float)transform[0], (float)transform[1], (float)transform[2]));
        var t = new double[] { matrix.M11, matrix.M12, matrix.M13, matrix.M14, matrix.M21, matrix.M22, matrix.M23, matrix.M24, matrix.M31, matrix.M32, matrix.M33, matrix.M34, matrix.M41, matrix.M42, matrix.M43, matrix.M44 };
        var root = GetRoot(geometricErrors[0], t, region, refine);
        var children = GetChildren(tiles, geometricErrors.Skip(1).ToArray(), minheight, maxheight);
        root.children = children;
        tileset.root = root;
        return tileset;
    }

    private static TileSet GetTilesetObject(Version version, double geometricError)
    {
        var tileset = new TileSet { asset = new Asset() { version = "1.1", generator = $"pg2b3dm {version}" } };
        tileset.geometricError = geometricError;
        return tileset;
    }

    private static Root GetRoot(double geometricError, double[] translation, double[] region, string refine="ADD")
    {
        var boundingVolume = new Boundingvolume {
            region = region
        };

        var root = new Root {
            geometricError = geometricError,
            refine = refine,
            transform = translation,
            boundingVolume = boundingVolume
        };

        return root;
    }

    private static List<Child> GetChildren(List<Tile> tiles, double[] geometricError, double minheight, double maxheight)
    {
        var children = new List<Child>();
        foreach (var tile in tiles) {
            if (tile.Available) {
                var child = GetChild(tile, geometricError[0], minheight, maxheight);

                if (tile.Children != null) {
                    child.children = GetChildren(tile.Children, geometricError.Skip(1).ToArray(), minheight, maxheight);
                }
                children.Add(child);
            }
        }

        return children;
    }

    public static Child GetChild(Tile tile, double geometricError, double minheight, double maxheight)
    {
        var child = new Child {
            geometricError = geometricError,
            content = new Content()
        };
        child.content.uri = $"content{Path.AltDirectorySeparatorChar}{tile.ContentUri}";

        var bbox = tile.BoundingBox;
        var boundingBox = new BoundingBox(bbox[0], bbox[1], bbox[2], bbox[3]);
        var region = boundingBox.ToRadians().ToRegion(minheight,maxheight);
        child.boundingVolume = new Boundingvolume {
            region = region
        };

        return child;
    }
}
